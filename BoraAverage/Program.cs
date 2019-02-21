using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeListDbContext;
using System.IO;
using CodeListDbContext.DbContext;
using SQLServerStockBunkatu;

namespace BoraAverage
{
    class Program
    {
        static void Main(string[] args)
        {
            var connect = new CodeListDbContext.FinanceConnect();
            var con = connect.GetContext();
            DayTRRanking(con);
            //WeekTRRanking(con);
            //MonthsTRRanking(con);
            //TRLatestHighPrice(con, 5, 10);
            //TRRanking(con,5, 2, 10.0);
            //BusinessDayHighLowBoraRanking(con, 1);
            //BoraRanking(con, 5);
        }
        //日付別にTRランキング作成
        static private void DayTRRanking(CodeListDbContext.DbContext.CodeListDbContext con)
        {
            var year = 2019;
            var month = 2;
            using (StreamWriter sw = new StreamWriter(string.Format(@"D:\デスクトップ\ボラデータ\日別ボラ\{0}_{1}.csv", year, month), false, Encoding.UTF8))
            {
                //ヘッダー書き込み
                var headerDayBoraItem = new DayBoraItem();
                var propertiesArray = headerDayBoraItem.GetType().GetProperties();
                foreach (var info in propertiesArray)
                {
                    sw.Write($"{info.Name},");
                }
                sw.WriteLine();
                var codeLists = con.CodeLists.ToList();

                var db = new GetBunkatuContext();
                var bunkatuCon = db.GetContext();

                foreach (var codeList in codeLists)
                {
                    //分割併合リスト
                    var bunkatuList = bunkatuCon.Bunkatus.Where(x => x.Code == codeList.code).ToList();
                    var heigouList = bunkatuCon.Heigous.Where(x => x.Code == codeList.code).ToList();
                    //株価データ
                    var tradeIndexs = con.TradeIndexs.Where(x => x.code == codeList.code).OrderBy(x => x.date).ToList();
                    //月別作成(monthが0ならフィルタなし)
                    //月始のインデックスを取得する
                    var monthTradeIndexs = tradeIndexs.ToList();
                    int monthIndex = 0;
                    if (month != 0)
                    {
                        var aaa = tradeIndexs.Select((value, index) => new { V = value, I = index }).Where(x => x.V.date.Year == year && x.V.date.Month == month).FirstOrDefault();
                        if(aaa != null)
                        {
                            monthIndex = aaa.I;
                        }
                        monthTradeIndexs = tradeIndexs.Where(x => (x.date.Month == month) && (x.date.Year == year)).OrderBy(x => x.date).ToList();
                    }
                    if(monthTradeIndexs.Count == 0)
                    {
                        continue;
                    }
                    
                    //5,10,20ボラ用の高値安値データ初期化
                    int queueCount = 20;
                    var queue = new Queue<QueueBoraItem>();
                    //処理する日付より前ののみ入れる
                    for(int i = monthIndex - queueCount; i < monthIndex; i++)
                    {
                        if(i < 0)
                        {
                            continue;
                        }
                        var item = new QueueBoraItem
                        {
                            Date = tradeIndexs[i].date,
                            HighPrice = tradeIndexs[i].price.highPrice,
                            LowPrice = tradeIndexs[i].price.lowPrice,
                            ClosePrice = tradeIndexs[i].price.closePrice,
                            HighCap = (long?)tradeIndexs[i].price.highPrice * tradeIndexs[i].outstandingShares,
                            LowCap = (long?)tradeIndexs[i].price.lowPrice * tradeIndexs[i].outstandingShares
                        };
                        var closeCount = i;
                        while(item.ClosePrice == null)
                        {
                            closeCount--;
                            if(closeCount < 0)
                            {
                                break;
                            }
                            item.ClosePrice = tradeIndexs[closeCount].price.closePrice;
                        }
                        queue.Enqueue(item);
                    }                    
                    if (tradeIndexs.Count == 0)
                    {
                        continue;
                    }
                    //メインデータ処理
                    var MA5ueCount = 0;

                    foreach (var tradeIndex in monthTradeIndexs)
                    {
                        var highPrice = tradeIndex.price.highPrice;
                        var lowPrice = tradeIndex.price.lowPrice;
                        var lastClosePrice = tradeIndex.price.lastClosePrice;
                        
                        //2,5,10,20用Queue処理
                        var day2HighData = new QueueBoraItem() { HighPrice = null, LowPrice = null };
                        var day2LowData = new QueueBoraItem() { HighPrice = null, LowPrice = null };
                        var day5HighData = new QueueBoraItem() { HighPrice = null, LowPrice = null };
                        var day5LowData = new QueueBoraItem() { HighPrice = null, LowPrice = null };
                        var day10HighData = new QueueBoraItem() { HighPrice = null, LowPrice = null };
                        var day10LowData = new QueueBoraItem() { HighPrice = null, LowPrice = null };
                        var day20HighData = new QueueBoraItem() { HighPrice = null, LowPrice = null };
                        var day20LowData = new QueueBoraItem() { HighPrice = null, LowPrice = null };

                        var day5HighCapData = new QueueBoraItem() { HighCap = null, LowCap = null };
                        var day5LowCapData = new QueueBoraItem() { HighCap = null, LowCap = null };

                        var item = new QueueBoraItem
                        {
                            Date = tradeIndex.date,
                            HighPrice = tradeIndex.price.highPrice,
                            LowPrice = tradeIndex.price.lowPrice,
                            ClosePrice = tradeIndex.price.closePrice,
                            HighCap = (long?)tradeIndex.price.highPrice * tradeIndex.outstandingShares,
                            LowCap = (long?)tradeIndex.price.lowPrice * tradeIndex.outstandingShares
                        };
                        queue.Enqueue(item);
                        //Queueを最大値まで減らす
                        while (queue.Count > queueCount)
                        {
                            queue.Dequeue();
                        }
                        //補正用キュー
                        var hoseiQueue = new Queue<QueueBoraItem>(queue);
                        //分割対応
                        if(bunkatuList.Count > 0)
                        {
                            //分割適応日付を跨いでいる場合は補正
                            for(int bunkatuIndex = 0; bunkatuIndex < bunkatuList.Count; bunkatuIndex++)
                            {
                                var checkDate = bunkatuList[bunkatuIndex].BaikyakuDate;
                                if (queue.First().Date <= checkDate && checkDate <= queue.Last().Date)
                                {
                                    hoseiQueue = HoseiBunkatuQueue(hoseiQueue, checkDate, bunkatuList[bunkatuIndex].BunkatuMae, bunkatuList[bunkatuIndex].BunkatuAto);
                                }
                                //前引け値分割対応
                                if(tradeIndex.date == checkDate)
                                {
                                    lastClosePrice = lastClosePrice * bunkatuList[bunkatuIndex].BunkatuMae / bunkatuList[bunkatuIndex].BunkatuAto;
                                }
                            }

                        }
                        //併合対応
                        if (heigouList.Count > 0)
                        {
                            //分割適応日付を跨いでいる場合は補正
                            for (int bunkatuIndex = 0; bunkatuIndex < heigouList.Count; bunkatuIndex++)
                            {
                                var checkDate = heigouList[bunkatuIndex].KenriDate;
                                if (queue.First().Date <= checkDate && checkDate < queue.Last().Date)
                                {
                                    hoseiQueue = HoseiHeigouQueue(hoseiQueue, checkDate, heigouList[bunkatuIndex].HeigouMae, heigouList[bunkatuIndex].HeigouAto);
                                }
                                //前引け値併合対応
                                if (queue.Count >= 2)
                                {
                                    if (queue.Skip(queue.Count - 2).First().Date == checkDate)
                                    {
                                        lastClosePrice = lastClosePrice / heigouList[bunkatuIndex].HeigouAto * heigouList[bunkatuIndex].HeigouMae;
                                    }
                                }
                            }                            
                        }
                        //TRデータ作成
                        var TRItem = CalcTR(highPrice, lowPrice, lastClosePrice);
                        //2日データ
                        if (queue.Count >= 2)
                        {
                            day2HighData = hoseiQueue.Skip(hoseiQueue.Count - 2).Where(x => x.HighPrice != null).Where(x => x.HighPrice == hoseiQueue.Skip(hoseiQueue.Count - 2).Where(z => z.HighPrice != null).Max(max => max.HighPrice).Value).LastOrDefault();
                            day2LowData = hoseiQueue.Skip(hoseiQueue.Count - 2).Where(x => x.LowPrice != null).Where(x => x.LowPrice == hoseiQueue.Skip(hoseiQueue.Count - 2).Where(z => z.LowPrice != null).Min(min => min.LowPrice).Value).LastOrDefault();
                        }
                        //5日データ 、5MA、時価総額ボラ
                        double? ma5 = null;
                        if (queue.Count >= 5)
                        {
                            ma5 = hoseiQueue.Skip(hoseiQueue.Count - 5).Select(x => x.ClosePrice).Average();
                            day5HighData = hoseiQueue.Skip(hoseiQueue.Count - 5).Where(x => x.HighPrice != null).Where(x => x.HighPrice == hoseiQueue.Skip(hoseiQueue.Count - 5).Where(z => z.HighPrice != null).Max(max => max.HighPrice).Value).LastOrDefault();
                            day5LowData = hoseiQueue.Skip(hoseiQueue.Count - 5).Where(x => x.LowPrice != null).Where(x => x.LowPrice == hoseiQueue.Skip(hoseiQueue.Count - 5).Where(z => z.LowPrice != null).Min(min => min.LowPrice).Value).LastOrDefault();
                            day5HighCapData = hoseiQueue.Skip(hoseiQueue.Count - 5).Where(x => x.HighCap != null).Where(x => x.HighCap == hoseiQueue.Skip(hoseiQueue.Count - 5).Where(z => z.HighCap != null).Max(max => max.HighCap).Value).LastOrDefault();
                            day5LowCapData = hoseiQueue.Skip(hoseiQueue.Count - 5).Where(x => x.LowCap != null).Where(x => x.LowCap == hoseiQueue.Skip(hoseiQueue.Count - 5).Where(z => z.LowCap != null).Min(min => min.LowCap).Value).LastOrDefault();
                        }
                        //5MAの上にいる連続日数
                        MA5ueCount++;
                        if (ma5 == null || ma5 > tradeIndex.price.closePrice)
                        {
                            MA5ueCount = 0;
                        }
                        //10日データ、10MA
                        double? ma10 = null;
                        if (queue.Count >= 10)
                        {
                            ma10 = hoseiQueue.Skip(hoseiQueue.Count - 10).Select(x => x.ClosePrice).Average();
                            day10HighData = hoseiQueue.Skip(hoseiQueue.Count - 10).Where(x => x.HighPrice != null).Where(x => x.HighPrice == hoseiQueue.Skip(hoseiQueue.Count - 10).Where(z => z.HighPrice != null).Max(max => max.HighPrice).Value).LastOrDefault();
                            day10LowData = hoseiQueue.Skip(hoseiQueue.Count - 10).Where(x => x.LowPrice != null).Where(x => x.LowPrice == hoseiQueue.Skip(hoseiQueue.Count - 10).Where(z => z.LowPrice != null).Min(min => min.LowPrice).Value).LastOrDefault();
                        }
                        //20日データ
                        if (queue.Count >= 5)
                        {
                            day20HighData = hoseiQueue.Where(x => x.HighPrice != null).Where(x => x.HighPrice == hoseiQueue.Where(z => z.HighPrice != null).Max(max => max.HighPrice).Value).LastOrDefault();
                            day20LowData = hoseiQueue.Where(x => x.LowPrice != null).Where(x => x.LowPrice == hoseiQueue.Where(z => z.LowPrice != null).Min(min => min.LowPrice).Value).LastOrDefault();
                        }

                        var highCap = (long?)highPrice * tradeIndex.outstandingShares;
                        var lowCap = (long?)lowPrice * tradeIndex.outstandingShares;
                        var openPrice = tradeIndex.price.openPrice;
                        var closePrice = tradeIndex.price.closePrice;
                        var lastCloseCap = (long?)lastClosePrice * tradeIndex.outstandingShares;
                        var openCap = (long?)openPrice * tradeIndex.outstandingShares;

                        var dayBoraItem = new DayBoraItem
                        {
                            Date = tradeIndex.date,
                            Code = tradeIndex.code,
                            Name = codeList.name,
                            ClosePrice = closePrice,
                            OpenPrice = openPrice,
                            HighPrice = highPrice,
                            LowPrice = lowPrice,
                            LastClosePrice = lastClosePrice,
                            Bora = highPrice - lowPrice,
                            BoraPercent = (highPrice - lowPrice) / lowPrice * 100,
                            TorihikiHiritu = (double)tradeIndex.price.volume / (double)tradeIndex.outstandingShares,
                            Cap = tradeIndex.marketCapitalization,
                            TR = TRItem.TR,
                            TRPercent = TRItem.TRPercent,
                            TRpattern = TRItem.TRPattern,
                            MaeHighPercent = (lastCloseCap == 0) ? null : (highPrice - lastClosePrice) / lastClosePrice * 100,
                            MaeLowPercent = (lastCloseCap == 0) ? null : (lowPrice - lastClosePrice) / lastClosePrice * 100,

                            OpenCap = openCap,
                            HighCap = highCap,
                            LowCap = lowCap,
                            LastCloseCap = lastCloseCap,
                            HighLowCapBora = highCap - lowCap,
                            HighLowCapPercent = (lastCloseCap == 0) ? null : ((double?)highCap - (double?)lowCap) / (double?)lastCloseCap * 100,
                            PlusCap = tradeIndex.marketCapitalization - lastCloseCap,

                            Day5HighCap = (day5HighCapData == null) ? null : day5HighCapData.HighCap,
                            Day5HighCapDate = (day5HighCapData == null) ? DateTime.Parse("1901/01/01") : day5HighCapData.Date,
                            Day5LowCap = (day5LowCapData == null) ? null : day5LowCapData.LowCap,
                            Day5LowCapDate = (day5LowCapData == null) ? DateTime.Parse("1901/01/01") : day5LowCapData.Date,
                            Day5CapBora = (day5HighCapData == null) ? null : day5HighCapData.HighCap - day5LowCapData.LowCap,
                            Day5CapBoraPercent = (day5HighCapData == null) ? null : ((double?)day5HighCapData.HighCap - (double?)day5LowCapData.LowCap) / (double?)day5LowCapData.LowCap * 100,

                            OpenCloseBora = closePrice - openPrice,
                            OpenCloseBoraPercent = (closePrice - openPrice)/openPrice*100,
                            OpenHighBora = highPrice - openPrice,
                            OpenHighBoraPercent = (highPrice - openPrice) / openPrice * 100,

                            Day2HighPrice = (day2HighData == null) ? null : day2HighData.HighPrice,
                            Day2HighPriceDate = (day2HighData == null) ? DateTime.Parse("1901/01/01") : day2HighData.Date,
                            Day2LowPrice = (day2LowData == null) ? null : day2LowData.LowPrice,
                            Day2LowPriceDate = (day2LowData == null) ? DateTime.Parse("1901/01/01") : day2LowData.Date,
                            Day2Bora = (day2HighData == null) ? null : day2HighData.HighPrice - day2LowData.LowPrice,
                            Day2BoraPercent = (day2HighData == null) ? null : (day2HighData.HighPrice - day2LowData.LowPrice) / day2LowData.LowPrice * 100,

                            Day5HighPrice = (day5HighData == null) ? null : day5HighData.HighPrice,
                            Day5HighPriceDate = (day5HighData == null) ? DateTime.Parse("1901/01/01") : day5HighData.Date,
                            Day5LowPrice = (day5LowData == null) ? null : day5LowData.LowPrice,
                            Day5LowPriceDate = (day5LowData == null) ? DateTime.Parse("1901/01/01") : day5LowData.Date,
                            Day5Bora = (day5HighData == null) ? null : day5HighData.HighPrice - day5LowData.LowPrice,
                            Day5BoraPercent = (day5HighData == null) ? null : (day5HighData.HighPrice - day5LowData.LowPrice) / day5LowData.LowPrice * 100,

                            Day10HighPrice = (day10HighData == null) ? null : day10HighData.HighPrice,
                            Day10HighPriceDate = (day10LowData == null) ? DateTime.Parse("1901/01/01") : day10HighData.Date,
                            Day10LowPrice = (day10HighData == null) ? null : day10LowData.LowPrice,
                            Day10LowPriceDate = (day10LowData == null) ? DateTime.Parse("1901/01/01") : day10LowData.Date,
                            Day10Bora = (day10HighData == null) ? null : day10HighData.HighPrice - day10LowData.LowPrice,
                            Day10BoraPercent = (day10HighData == null) ? null : (day10HighData.HighPrice - day10LowData.LowPrice) / day10LowData.LowPrice * 100,

                            Day20HighPrice = (day20HighData == null) ? null : day20HighData.HighPrice,
                            Day20HighPriceDate = (day20LowData == null) ? DateTime.Parse("1901/01/01") : day20HighData.Date,
                            Day20LowPrice = (day20HighData == null) ? null : day20LowData.LowPrice,
                            Day20LowPriceDate = (day20LowData == null) ? DateTime.Parse("1901/01/01") : day20LowData.Date,
                            Day20Bora = (day20HighData == null) ? null : day20HighData.HighPrice - day20LowData.LowPrice,
                            Day20BoraPercent = (day20HighData == null) ? null : (day20HighData.HighPrice - day20LowData.LowPrice) / day20LowData.LowPrice * 100,

                            MA5 = ma5,
                            MA5Kairi = (tradeIndex.price.closePrice - ma5) / ma5 * 100,
                            MA5UeCount = MA5ueCount,

                            MA10 = ma10,
                            MA10Kairi = (tradeIndex.price.closePrice - ma10) / ma10 * 100
                        };
                        //csv書き込み
                        foreach (var info in propertiesArray)
                        {
                            sw.Write($"{info.GetValue(dayBoraItem, null)},");
                        }
                        sw.WriteLine();
                    }
                    Console.WriteLine(codeList.code);
                }
            }
        }
        //1週毎にTRランキング作成
        static private void WeekTRRanking(CodeListDbContext.DbContext.CodeListDbContext con)
        {
            var year = 2018;
            using (StreamWriter sw = new StreamWriter(string.Format(@"D:\デスクトップ\ボラデータ\週別ボラ\{0}.csv", year), false, Encoding.UTF8))
            {
                //ヘッダー書き込み
                var weekBoraItem = new WeekBoraItem();
                var propertiesArray = weekBoraItem.GetType().GetProperties();
                foreach (var info in propertiesArray)
                {
                    sw.Write($"{info.Name},");
                }
                sw.WriteLine();
                var codeLists = con.CodeLists.ToList();
                var db = new GetBunkatuContext();
                var bunkatuCon = db.GetContext();
                foreach (var codeList in codeLists)
                {
                    //分割併合リスト
                    var bunkatuList = bunkatuCon.Bunkatus.Where(x => x.Code == codeList.code).ToList();
                    var heigouList = bunkatuCon.Heigous.Where(x => x.Code == codeList.code).ToList();

                    var tradeIndexs = con.TradeIndexs.Where(x => x.code == codeList.code).OrderBy(x => x.date).ToList();
                    if(tradeIndexs.Count == 0)
                    {
                        continue;
                    }
                    var startDate = tradeIndexs.FirstOrDefault().date;
                    var endDate = tradeIndexs.FirstOrDefault().date;
                    var lastDate = tradeIndexs.LastOrDefault().date;
                    var highPriceList = new Dictionary<DateTime, double?>();
                    var lowPriceList = new Dictionary<DateTime, double?>();                    
                    foreach (var tradeIndex in tradeIndexs)
                    {        
                        //2日以上間が空けば登録
                        if((tradeIndex.date - endDate).TotalDays >= 2)
                        {
                            var highPriceDic = highPriceList.OrderByDescending(x => x.Value).FirstOrDefault();
                            var lowPriceDic = lowPriceList.OrderByDescending(x => x.Value).LastOrDefault();
                            var WeekBoraItem = new WeekBoraItem
                            {
                                StartDate = startDate,
                                EndDate = endDate,
                                Code = tradeIndex.code,
                                Name = tradeIndex.price.codeList.name,
                                HighPrice = highPriceDic.Value,
                                HighPriceDate = highPriceDic.Key,
                                LowPrice = lowPriceDic.Value,
                                LowPriceDate = lowPriceDic.Key,
                                Bora = highPriceDic.Value - lowPriceDic.Value,
                                BoraPercent = (highPriceDic.Value - lowPriceDic.Value) / lowPriceDic.Value * 100,
                            };
                            //csv書き込み
                            foreach (var info in propertiesArray)
                            {
                                sw.Write($"{info.GetValue(WeekBoraItem, null)},");
                            }
                            sw.WriteLine();
                            //高値安値削除
                            highPriceList.Clear();
                            lowPriceList.Clear();
                            //日付周りの処理
                            startDate = tradeIndex.date;                            
                        }
                        //通常処理
                        highPriceList.Add(tradeIndex.date, tradeIndex.price.highPrice);
                        lowPriceList.Add(tradeIndex.date, tradeIndex.price.lowPrice);
                        endDate = tradeIndex.date;
                        //最後の日付の場合登録
                        if (lastDate == tradeIndex.date)
                        {
                            var highPriceDic = highPriceList.Where(x => x.Value != null).OrderByDescending(x => x.Value).FirstOrDefault();
                            var lowPriceDic = lowPriceList.Where(x => x.Value != null).OrderByDescending(x => x.Value).LastOrDefault();

                            var WeekBoraItem = new WeekBoraItem
                            {
                                StartDate = startDate,
                                EndDate = endDate,
                                Code = tradeIndex.code,
                                Name = tradeIndex.price.codeList.name,
                                HighPrice = highPriceDic.Value,
                                HighPriceDate = highPriceDic.Key,
                                LowPrice = lowPriceDic.Value,
                                LowPriceDate = lowPriceDic.Key,
                                Bora = highPriceDic.Value - lowPriceDic.Value,
                                BoraPercent = (highPriceDic.Value - lowPriceDic.Value) / lowPriceDic.Value * 100,
                            };
                            //csv書き込み
                            foreach (var info in propertiesArray)
                            {
                                sw.Write($"{info.GetValue(WeekBoraItem, null)},");
                            }
                            sw.WriteLine();
                        }
                    }
                    Console.WriteLine(codeList.code);
                }
            }
        }
        //月別でTRランキングを作る
        static private void MonthsTRRanking(CodeListDbContext.DbContext.CodeListDbContext con)
        {
            var year = 2018;
            var tradeIndexs = con.TradeIndexs.Where(x => x.date.Year == year).ToList();
            var monthsList = tradeIndexs.Select(x => x.date.Month).Distinct().ToList();
            
            using (StreamWriter sw = new StreamWriter(string.Format(@"D:\デスクトップ\ボラデータ\月別ボラ\{0}.csv",year), false, Encoding.UTF8))
            {
                //ヘッダー書き込み
                var monthsBoraitem = new MonthsBoraitem();
                var propertiesArray = monthsBoraitem.GetType().GetProperties();
                foreach (var info in propertiesArray)
                {
                    sw.Write($"{info.Name},");
                }
                sw.WriteLine();
                foreach (var month in monthsList)
                {
                    var monthTradeIndex = tradeIndexs.Where(x => x.date.Month == month).ToList();
                    var codesList = monthTradeIndex.Select(x => x.code).Distinct().ToList();
                    foreach (var code in codesList)
                    {
                        var item = monthTradeIndex.Where(x => x.code == code).OrderBy(x => x.date).ToList();
                        var kizyunOutstandingShares = item.Last().outstandingShares;
                        var highPriceList = new Dictionary<DateTime, double?>();
                        var lowPriceList = new Dictionary<DateTime, double?>();
                        foreach (var tradeIndex in item)
                        {
                            highPriceList.Add(tradeIndex.date, tradeIndex.price.highPrice);
                            lowPriceList.Add(tradeIndex.date, tradeIndex.price.lowPrice);
                        }
                        var highPrice = highPriceList.Where(x => x.Value != null).OrderByDescending(x => x.Value).FirstOrDefault();
                        var lowPrice = lowPriceList.Where(x => x.Value != null).OrderByDescending(x => x.Value).LastOrDefault();
                        var MonthBoraItem = new MonthsBoraitem
                        {
                            Month = month,
                            Code = code,
                            Name = item.Last().price.codeList.name,
                            HighPrice = highPrice.Value,
                            HighPriceDate = highPrice.Key,
                            LowPrice = lowPrice.Value,
                            LowPriceDate = lowPrice.Key,
                            Bora = highPrice.Value - lowPrice.Value,
                            BoraPercent = (highPrice.Value - lowPrice.Value) / lowPrice.Value * 100   ,
                        };
                        //csv書き込み
                        foreach (var info in propertiesArray)
                        {
                            sw.Write($"{info.GetValue(MonthBoraItem, null)},");
                        }
                        sw.WriteLine();
                        Console.WriteLine($"{month}, {code}");
                    }
                }
            }

            
        }
        //〇営業日以内にTR〇%以上時の高値を超えた銘柄
        static private void TRLatestHighPrice(CodeListDbContext.DbContext.CodeListDbContext con, int businessDay, double OverTRPercent)
        {
            var path = string.Format(@"D:\デスクトップ\ボラデータ\{0}_{1}営業日_{2}%TRLatestHighPrice.csv", DateTime.Now.ToString("yyyyMMdd"), businessDay,  OverTRPercent);
            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                //ヘッダー書き込み
                var headerTRItem = new TRItemAddTRHighPrice();
                var propertiesArray = headerTRItem.GetType().GetProperties();
                foreach (var info in propertiesArray)
                {
                    sw.Write($"{info.Name},");
                }
                sw.WriteLine();

                var codeLists = con.CodeLists.ToList();
                foreach (var codeList in codeLists)
                {
                    var queTRItem = new Queue<TRItemAddTRHighPrice>(businessDay);
                    string text = "";
                    var code = codeList.code;
                    var tradeIndexs = con.TradeIndexs.Where(x => x.code == code).ToList();
                    tradeIndexs = tradeIndexs.OrderBy(x => x.date).ToList();
                    foreach (var tradeIndex in tradeIndexs)
                    {
                        var item = new TRItemAddTRHighPrice
                        {
                            Date = tradeIndex.date,
                            Code = tradeIndex.code,
                            Name = tradeIndex.price.codeList.name,
                            ClosePrice = tradeIndex.price.closePrice,
                            OpenPrice = tradeIndex.price.openPrice,
                            HighPrice = tradeIndex.price.highPrice,
                            LowPrice = tradeIndex.price.lowPrice,
                            LastClosePrice = tradeIndex.price.lastClosePrice,
                            Volume = tradeIndex.price.volume,
                            Capitalization = tradeIndex.marketCapitalization,
                            TorihikiHiritu = (double)tradeIndex.price.volume / (double)tradeIndex.outstandingShares,
                        };
                        var TRItem = CalcTR(tradeIndex.price.highPrice, tradeIndex.price.lowPrice, tradeIndex.price.lastClosePrice, OverTRPercent);
                        item.TR = TRItem.TR;
                        item.TRCount = 0;
                        item.TRPattern = TRItem.TRPattern;
                        item.TRPercent = TRItem.TRPercent;
                        item.TRHighPrice = TRItem.HighPrice;

                        //キュー入れる前に高値判定
                        var checkHighPrice = queTRItem.Select(x => x.TRHighPrice).Max();
                        //キュー挿入
                        queTRItem.Enqueue(item);
                        if (queTRItem.Count > businessDay)
                        {
                            queTRItem.Dequeue();
                        }
                        item.TRCount = queTRItem.Where(x => x.TRPercent >= OverTRPercent).Count();
                        //高値更新していたら書き込み
                        if (checkHighPrice < item.HighPrice && checkHighPrice != null)
                        {
                            //csv書き込み
                            foreach (var info in propertiesArray)
                            {
                                text += $"{info.GetValue(item, null)},";
                            }
                            text += "\r\n";
                        }
                    }
                    sw.Write(text);
                    Console.WriteLine(code);
                }
            }
        }
        //〇営業日以内にTRが〇回以上入った銘柄
        static private void TRRanking(CodeListDbContext.DbContext.CodeListDbContext con, int businessDay, int countDay, double OverTRPercent)
        {
            var path = string.Format(@"D:\デスクトップ\ボラデータ\{0}_TR{1}営業日_{3}ﾊﾟｰｾﾝﾄ{2}回以上.csv", DateTime.Now.ToString("yyyyMMdd"), businessDay, countDay, OverTRPercent);
            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                //ヘッダー書き込み
                var headerTRItem = new TRItem();
                var propertiesArray = headerTRItem.GetType().GetProperties();
                foreach (var info in propertiesArray)
                {
                    sw.Write($"{info.Name},");
                }
                sw.WriteLine();

                var codeLists = con.CodeLists.ToList();
                foreach (var codeList in codeLists)
                {
                    var queTRItem = new Queue<TRItem>(businessDay);
                    string text = "";
                    var code = codeList.code;
                    var tradeIndexs = con.TradeIndexs.Where(x => x.code == code).ToList();
                    tradeIndexs = tradeIndexs.OrderBy(x => x.date).ToList();
                    foreach (var tradeIndex in tradeIndexs)
                    {
                        var item = new TRItem
                        {                            
                            Date = tradeIndex.date,
                            Code = tradeIndex.code,
                            Name = tradeIndex.price.codeList.name,
                            ClosePrice = tradeIndex.price.closePrice,
                            OpenPrice = tradeIndex.price.openPrice,
                            HighPrice = tradeIndex.price.highPrice,
                            LowPrice = tradeIndex.price.lowPrice,
                            LastClosePrice = tradeIndex.price.lastClosePrice,
                            Volume = tradeIndex.price.volume,
                            Capitalization = tradeIndex.marketCapitalization,
                            TorihikiHiritu = (double)tradeIndex.price.volume / (double)tradeIndex.outstandingShares,                            
                        };
                        var TRItem = CalcTR(tradeIndex.price.highPrice, tradeIndex.price.lowPrice, tradeIndex.price.lastClosePrice);
                        item.TR = TRItem.TR;
                        item.TRCount = 0;
                        item.TRPattern = TRItem.TRPattern;
                        item.TRPercent = TRItem.TRPercent;
                        queTRItem.Enqueue(item);
                        if (queTRItem.Count > businessDay)
                        {
                            queTRItem.Dequeue();
                        }
                        item.TRCount = queTRItem.Where(x => x.TRPercent >= OverTRPercent).Count();
                        if(item.TRCount >= countDay)
                        {
                            //csv書き込み
                            foreach (var info in propertiesArray)
                            {
                                text += $"{info.GetValue(item, null)},";
                            }
                            text += "\r\n";
                        }
                    }
                    sw.Write(text);
                    Console.WriteLine(code);
                }
            }
        }
        //指定日の週別ボラ計算(未完成)
        static private void WeekBoraToDateCalc(DateTime datetime, List<CodeListDbContext.Finance.TradeIndex> tradeIndexs)
        {
            tradeIndexs = tradeIndexs.OrderBy(x => x.date).ToList();
            var checkDate = datetime;            
            var kizyunIndex = tradeIndexs.Select((value, index) => new { V = value, I = index }).First(x => x.V.date == datetime).I;
            var highPrice = tradeIndexs[kizyunIndex].price.highPrice;
            for (int i = kizyunIndex; i < 0; i--)
            {

                if((checkDate - tradeIndexs[i].date).TotalDays >= 2)
                {

                }
                checkDate = tradeIndexs[i].date;
            }
        }        
        //分割対応計算
        static private Queue<QueueBoraItem> HoseiBunkatuQueue(Queue<QueueBoraItem> queue, DateTime kizyunDateTime, double mae, double ato)
        {
            var result = new Queue<QueueBoraItem>();
            foreach(var que in queue)
            {
                var item = new QueueBoraItem()
                {
                    Date = que.Date,
                    HighPrice = que.HighPrice,
                    LowPrice = que.LowPrice,
                    ClosePrice = que.ClosePrice,
                    HighCap = que.HighCap,
                    LowCap = que.LowCap
                };
                if (que.Date < kizyunDateTime)
                {
                    item.HighPrice = que.HighPrice / ato * mae;
                    item.LowPrice = que.LowPrice / ato * mae;
                    item.ClosePrice = que.ClosePrice / ato * mae;
                }
                result.Enqueue(item);
            }
            return result;
        }
        //併合対応
        static private Queue<QueueBoraItem> HoseiHeigouQueue(Queue<QueueBoraItem> queue, DateTime kizyunDateTime, double mae, double ato)
        {
            var result = new Queue<QueueBoraItem>();
            foreach (var que in queue)
            {
                var item = new QueueBoraItem()
                {
                    Date = que.Date,
                    HighPrice = que.HighPrice,
                    LowPrice = que.LowPrice,
                    ClosePrice = que.ClosePrice,
                    HighCap = que.HighCap,
                    LowCap = que.LowCap
                };
                if (que.Date <= kizyunDateTime)
                {
                    item.HighPrice = que.HighPrice * mae / ato;
                    item.LowPrice = que.LowPrice * mae / ato;
                    item.ClosePrice = que.ClosePrice * mae / ato;
                }
                result.Enqueue(item);
            }
            return result;
        }
        //TR各種計算
        static private TRPatternItem CalcTR(double? highPrice, double? lowPrice, double? lastClosePrice)
        {
            string[] titleArray = { "高-安", "高-前引", "前引-安", };
            var TRList = new List<double?>
            {
                highPrice - lowPrice,
                highPrice - lastClosePrice,
                lastClosePrice - lowPrice
            };
            var i = TRList.Select((val, idx) => new { V = val, I = idx }).Aggregate((max, working) => (max.V > working.V) ? max : working).I;
            var result = new TRPatternItem
            {
                TR = TRList[i],
                TRPattern = titleArray[i],
                TRPercent = null,
            };
            switch(i)
            {
                case 0:
                    result.TRPercent = TRList[i] / lowPrice * 100;
                    break;
                case 1:
                    result.TRPercent = TRList[i] / lastClosePrice * 100;
                    break;
                case 2:
                    result.TRPercent = TRList[i] / lowPrice * 100;
                    break;
                default:
                    break;
            }   
            return result;
        }
        //条件高値付きTR各種計算
        static private TRPatternItem CalcTR(double? highPrice, double? lowPrice, double? lastClosePrice, double? TRPercentAddHighPrice)
        {
            string[] titleArray = { "高-安", "高-前引", "前引-安", };
            var TRList = new List<double?>
            {
                highPrice - lowPrice,
                highPrice - lastClosePrice,
                lastClosePrice - lowPrice
            };
            var i = TRList.Select((val, idx) => new { V = val, I = idx }).Aggregate((max, working) => (max.V > working.V) ? max : working).I;
            var result = new TRPatternItem
            {
                TR = TRList[i],
                TRPattern = titleArray[i],
                TRPercent = null,
                HighPrice = null
            };
            switch (i)
            {
                case 0:
                    result.TRPercent = TRList[i] / lowPrice * 100;
                    if(TRPercentAddHighPrice <= result.TRPercent)
                    {
                        result.HighPrice = highPrice;
                    }
                    break;
                case 1:
                    result.TRPercent = TRList[i] / lastClosePrice * 100;
                    if (TRPercentAddHighPrice <= result.TRPercent)
                    {
                        result.HighPrice = highPrice;
                    }
                    break;
                case 2:
                    result.TRPercent = TRList[i] / lowPrice * 100;
                    break;
                default:
                    break;
            }
            return result;
        }
        //〇営業日の高安値比率をランキングにする
        static private void BusinessDayHighLowBoraRanking(CodeListDbContext.DbContext.CodeListDbContext con, int businessDay)
        {            
            var path = string.Format(@"D:\デスクトップ\ボラデータ\{0}_高安幅_{1}営業日.csv", DateTime.Now.ToString("yyyyMMdd"), businessDay);            
            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                //ヘッダー書き込み
                var highLowBoraItem = new HighLowBoraItem();
                var propertiesArray = highLowBoraItem.GetType().GetProperties();
                foreach (var info in propertiesArray)
                {
                    sw.Write($"{info.Name},");
                }
                sw.WriteLine();
                
                var codeLists = con.CodeLists.ToList();
                int i = 0;
                foreach (var codeList in codeLists)
                {
                    var queTradeIndexs = new Queue<CodeListDbContext.Finance.TradeIndex>(businessDay);
                    string text = "";
                    
                    var code = codeList.code;
                    var tradeIndexs = con.TradeIndexs.Where(x => x.code == code).ToList();
                    tradeIndexs = tradeIndexs.OrderBy(x => x.date).ToList();
                    foreach (var tradeIndex in tradeIndexs)
                    {
                        var item = new HighLowBoraItem
                        {
                            Date = tradeIndex.date,
                            Code = tradeIndex.code,                            
                            
                            Name = tradeIndex.price.codeList.name,
                            HighPrice = tradeIndex.price.highPrice,
                            LowPrice = tradeIndex.price.lowPrice,
                            OpenPrice = tradeIndex.price.openPrice,
                            ClosePrice = tradeIndex.price.closePrice,
                            volume = tradeIndex.price.volume,
                            Capitalization = tradeIndex.marketCapitalization
                        };                            
                        
                        queTradeIndexs.Enqueue(tradeIndex);
                        if(queTradeIndexs.Count > businessDay)
                        {
                            queTradeIndexs.Dequeue();
                        }
                        var LatestHighPriceBunkatu = new List<BunkatuItem>();
                        var outstandingShares = tradeIndex.outstandingShares;
                        foreach (var que in queTradeIndexs)
                        {                            
                            var bairitu = (double)outstandingShares / (double)que.outstandingShares;
                            if(bairitu <= 0 || Double.IsNaN(bairitu))
                            {
                                bairitu = 1;
                            }
                            var bunkatuItem = new BunkatuItem
                            {
                                Date = que.date,
                                HighPrice = que.price.highPrice / bairitu,
                                LowPrice = que.price.lowPrice / bairitu
                            };
                            LatestHighPriceBunkatu.Add(bunkatuItem);
                        }
                        /*
                        var LatestHighPriceTradeIndex = queTradeIndexs.Where(x => x.price.highPrice == queTradeIndexs.Select(y => y.price.highPrice).Max()).LastOrDefault();
                        var LatestLowPriceTradeIndex = queTradeIndexs.Where(x => x.price.lowPrice == queTradeIndexs.Select(y => y.price.lowPrice).Min()).LastOrDefault();
                        item.StartDate = queTradeIndexs.Peek().date;
                        var nehaba = LatestHighPriceTradeIndex.price.highPrice - LatestHighPriceTradeIndex.price.lowPrice;

                        item.Nehaba = nehaba;
                        item.NehabaPercent = nehaba / LatestLowPriceTradeIndex.price.lowPrice * 100;
                        item.LatestHighPrice = LatestHighPriceTradeIndex.price.highPrice;
                        item.LatestHighPriceDate = LatestHighPriceTradeIndex.date;
                        item.LatestLowPrice = LatestLowPriceTradeIndex.price.lowPrice;
                        item.LatestLowPriceDate = LatestLowPriceTradeIndex.date;
                        */
                        var LatestHighPriceTradeIndex = LatestHighPriceBunkatu.Where(x => x.HighPrice == LatestHighPriceBunkatu.Select(y => y.HighPrice).Max()).LastOrDefault();
                        var LatestLowPriceTradeIndex = LatestHighPriceBunkatu.Where(x => x.LowPrice == LatestHighPriceBunkatu.Select(y => y.LowPrice).Min()).LastOrDefault();
                        item.StartDate = queTradeIndexs.Peek().date;
                        var nehaba = LatestHighPriceTradeIndex.HighPrice - LatestLowPriceTradeIndex.LowPrice;

                        item.Nehaba = nehaba;
                        item.NehabaPercent = nehaba / LatestLowPriceTradeIndex.LowPrice * 100;
                        item.LatestHighPrice = LatestHighPriceTradeIndex.HighPrice;
                        item.LatestHighPriceDate = LatestHighPriceTradeIndex.Date;
                        item.LatestLowPrice = LatestLowPriceTradeIndex.LowPrice;
                        item.LatestLowPriceDate = LatestLowPriceTradeIndex.Date;
                        //csv書き込み
                        foreach (var info in propertiesArray)
                        {
                            text += $"{info.GetValue(item, null)},";                            
                        }
                        text += "\r\n";    
                    }
                    sw.Write(text);
                    Console.WriteLine(code);
                }
            }
        }
        //5～20日間ボラ高値安値を返す
        //未完成
        static private void LatestHighCheck(CodeListDbContext.DbContext.CodeListDbContext con, int businessDay, double filterHighLastClosePercent)
        {
            var queHighPrice = new Queue<double?>(businessDay);
            var path = string.Format(@"D:\デスクトップ\ボラデータ\{0}_高値更新.csv", DateTime.Now.ToString("yyyyMMdd"));
            var codeLists = con.CodeLists.ToList();

            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                foreach (var codeList in codeLists)
                {
                    var code = codeList.code;
                    var tradeIndexs = con.TradeIndexs.Where(x => x.code == code).ToList();
                    tradeIndexs = tradeIndexs.OrderBy(x => x.date).ToList();
                    foreach(var tradeIndex in tradeIndexs)
                    {

                    }
                }
            }
        }
        static private void BoraRanking(CodeListDbContext.DbContext.CodeListDbContext con, int ma)
        {
            var path = string.Format(@"D:\デスクトップ\ボラデータ\{0}.csv", DateTime.Now.ToString("yyyyMMdd"));
            
            var codeLists = con.CodeLists.ToList();
            List<BoraItem> outputList = new List<BoraItem>();
            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                //ヘッダー書き込み
                var boraItem = new BoraItem();
                var propertiesArray = boraItem.GetType().GetProperties();
                foreach (var info in propertiesArray)
                {
                    sw.Write($"{info.Name},");
                }
                sw.WriteLine();                
                //ボラ情報書き込み
                foreach (var codeList in codeLists)
                {
                    string text = "";
                    var code = codeList.code;
                    //ボラリスト
                    var boraList = HighLowBora(con, code, ma);
                    //var tradeIndex = con.TradeIndexs.Where(x => x.code == code).ToList();
                    //tradeIndex = tradeIndex.OrderBy(x => x.date).ToList();
                    foreach(var bora in boraList)
                    {
                        foreach(var info in propertiesArray)
                        {
                            text += $"{info.GetValue(bora, null)},";
                        }
                        text += "\r\n";
                    }
                    sw.Write(text);
                    Console.WriteLine(code);
                }
                
            }                
        }
        static private List<BoraItem> HighLowBora(CodeListDbContext.DbContext.CodeListDbContext con, int code, int ma)
        {
            //ボラ
            var boraQue = new Queue<double?>(ma);
            //ボラ率
            var boraPercentQue = new Queue<double?>(ma);
            //戻り値
            var result = new List<BoraItem>();
            var tradeIndexs = con.TradeIndexs.Where(x => x.code == code).ToList();
            tradeIndexs = tradeIndexs.OrderBy(x => x.date).ToList();
            foreach(var tradeIndex in tradeIndexs)
            {
                var bora = tradeIndex.price.highPrice - tradeIndex.price.lowPrice;
                //(パーセント計算)
                double? boraAverage = boraAverage = bora / tradeIndex.price.lastClosePrice * 100;

                var boraItem = new BoraItem {
                    Date = tradeIndex.date,
                    Code = tradeIndex.code,
                    BoraAverage = null,
                    BoraPercentAverage = null,

                    Name = tradeIndex.price.codeList.name,
                    HighPrice = tradeIndex.price.highPrice,
                    LowPrice = tradeIndex.price.lowPrice,
                    OpenPrice = tradeIndex.price.openPrice,
                    ClosePrice = tradeIndex.price.closePrice,
                    volume = tradeIndex.price.volume,
                    Capitalization = tradeIndex.marketCapitalization
                };
                //値が付かないケースは0にする
                if(bora == null)
                {
                    bora = 0;
                }
                //ボラ率の計算
                if(bora == 0)
                {
                    boraAverage = 0;
                }               
                //キューへ入れる
                boraQue.Enqueue(bora);
                boraPercentQue.Enqueue(boraAverage);
                //ma以上になったら古いのから消す（boraQue）
                if (boraQue.Count > ma)
                {
                    boraQue.Dequeue();
                }
                //ma以上になったら古いのから消す（boraPercentQue）
                if (boraPercentQue.Count > ma)
                {
                    boraPercentQue.Dequeue();
                }
                //ma以下は値がnullのものを返す
                if (boraQue.Count < ma)
                {
                    result.Add(boraItem);
                    continue;
                }
                //合計が0は中身0で返す
                if(boraQue.Sum() == 0)
                {
                    boraItem.BoraAverage = 0;
                    boraItem.BoraPercentAverage = 0;
                    result.Add(boraItem);
                    continue;
                }
                //ボラ計算
                boraItem.BoraAverage = boraQue.Average();
                boraItem.BoraPercentAverage = boraPercentQue.Average();
                result.Add(boraItem);
            }
            return result;
        }
        //日付・高値・安値アイテム
        public class QueueBoraItem
        {
            public DateTime Date { get; set; }
            public double? HighPrice { get; set; }
            public double? LowPrice { get; set; }
            public double? ClosePrice { get; set; }
            public long? HighCap { get; set; }
            public long? LowCap { get; set; }
        }
        //日別ボラアイテム
        public class DayBoraItem
        {
            public DateTime Date { get; set; }
            public int Code { get; set; }
            public string Name { get; set; }
            public double? ClosePrice { get; set; }
            public double? OpenPrice { get; set; }
            public double? HighPrice { get; set; }            
            public double? LowPrice { get; set; }            
            public double? LastClosePrice { get; set; }
            public double? OpenCap { get; set; }
            public long? HighCap { get; set; }
            public long? LowCap { get; set; }
            public long? LastCloseCap { get; set; }
            public long Cap { get; set; }
            public long? PlusCap { get; set; }
            public double? HighLowCapBora { get; set; }
            public double? HighLowCapPercent { get; set; }

            public double? TorihikiHiritu { get; set; }            
            public double? TR { get; set; }
            public double? TRPercent { get; set; }
            public string TRpattern { get; set; }
            public double? Bora { get; set; }
            public double? BoraPercent { get; set; }
            public double? MaeHighPercent { get; set; }
            public double? MaeLowPercent { get; set; }
            //5日分の時価総額ボラ
            public double? Day5HighCap { get; set; }
            public DateTime Day5HighCapDate { get; set; }
            public double? Day5LowCap { get; set; }
            public DateTime Day5LowCapDate { get; set; }
            public double? Day5CapBora { get; set; }
            public double? Day5CapBoraPercent { get; set; }
            //当日の始値-高値ボラ
            public double? OpenHighBora { get; set; }
            public double? OpenHighBoraPercent { get; set; }
            //当日の始値-終値ボラ
            public double? OpenCloseBora { get; set; }
            public double? OpenCloseBoraPercent { get; set; }
            //前日～当日のボラ
            public double? Day2HighPrice { get; set; }
            public DateTime Day2HighPriceDate { get; set; }
            public double? Day2LowPrice { get; set; }
            public DateTime Day2LowPriceDate { get; set; }
            public double? Day2Bora { get; set; }
            public double? Day2BoraPercent { get; set; }
            //5日分のボラ
            public double? Day5HighPrice { get; set; }
            public DateTime Day5HighPriceDate { get; set; }
            public double? Day5LowPrice { get; set; }
            public DateTime Day5LowPriceDate { get; set; }
            public double? Day5Bora { get; set; }
            public double? Day5BoraPercent { get; set; }
            //10日分のボラ
            public double? Day10HighPrice { get; set; }
            public DateTime Day10HighPriceDate { get; set; }
            public double? Day10LowPrice { get; set; }
            public DateTime Day10LowPriceDate { get; set; }
            public double? Day10Bora { get; set; }
            public double? Day10BoraPercent { get; set; }
            //20日分のボラ
            public double? Day20HighPrice { get; set; }
            public DateTime Day20HighPriceDate { get; set; }
            public double? Day20LowPrice { get; set; }
            public DateTime Day20LowPriceDate { get; set; }
            public double? Day20Bora { get; set; }
            public double? Day20BoraPercent { get; set; }
            //5MA
            public double? MA5 { get; set; }
            public double? MA5Kairi { get; set; }
            public int MA5UeCount { get; set; }
            //10MA
            public double? MA10 { get; set; }
            public double? MA10Kairi { get; set; }
            //public double? WeekBora { get; set; }
            //public double? WeekBoraPercent { get; set; }
            //public DateTime WeekBoraDate { get; set; }
            //public double? MonthBora { get; set; }
            //public double? MonthBoraPercent { get; set; }
            //public DateTime MonthBoraDate { get; set; }
            
        }
        //週別ボラアイテム
        public class WeekBoraItem
        {
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public int Code { get; set; }
            public string Name { get; set; }
            public double? HighPrice { get; set; }
            public DateTime HighPriceDate { get; set; }
            public double? LowPrice { get; set; }
            public DateTime LowPriceDate { get; set; }
            public double? Bora { get; set; }
            public double? BoraPercent { get; set; }
        }
        //月別ボラアイテム
        public class MonthsBoraitem
        {
            public int Month { get; set; }
            public int Code { get; set; }
            public string Name { get; set; }
            public double? HighPrice { get; set; }
            public DateTime HighPriceDate { get; set; }
            public double? LowPrice { get; set; }
            public DateTime LowPriceDate { get; set; }
            public double? Bora { get; set; }
            public double? BoraPercent { get; set; }
        }
        public class TRPatternItem
        {
            public double? TR { get; set; }
            public double? TRPercent { get; set; }
            public string TRPattern { get; set; }
            public double? HighPrice { get; set;
            }
        }
        public class TRItemAddTRHighPrice
        {
            public DateTime Date { get; set; }
            public int Code { get; set; }
            public string Name { get; set; }
            public int TRCount { get; set; }
            public double? TR { get; set; }
            public double? TRPercent { get; set; }
            public string TRPattern { get; set; }
            public double TorihikiHiritu { get; set; }
            public double? ClosePrice { get; set; }
            public double? OpenPrice { get; set; }
            public double? HighPrice { get; set; }
            public double? LowPrice { get; set; }
            public double? LastClosePrice { get; set; }
            public double? TRHighPrice { get; set; }
            public long Volume { get; set; }
            public long Capitalization { get; set; }
        }
        public class TRItem
        {
            public DateTime Date { get; set; }
            public int Code { get; set; }
            public string Name { get; set; }
            public int TRCount { get; set; }
            public double? TR { get; set; }
            public double? TRPercent { get; set; }
            public string TRPattern { get; set; }
            public double TorihikiHiritu { get; set; }
            public double? ClosePrice { get; set; }
            public double? OpenPrice { get; set; }
            public double? HighPrice { get; set; }
            public double? LowPrice { get; set; }
            public double? LastClosePrice { get; set; }
            public long Volume { get; set; }
            public long Capitalization { get; set; }
        }
        public class BunkatuItem
        {
            public DateTime Date { get; set; }
            public double? HighPrice { get; set; }
            public double? LowPrice { get; set; }
        }
        public class HighLowBoraItem
        {
            public DateTime StartDate { get; set; }
            public DateTime Date { get; set; }
            public int Code { get; set; }
            public string Name { get; set; }

            public double? Nehaba { get; set; }
            public double? NehabaPercent { get; set; }
            public double? LatestHighPrice { get; set; }
            public DateTime LatestHighPriceDate { get; set; }
            public double? LatestLowPrice { get; set; }
            public DateTime LatestLowPriceDate { get; set; }

            public double? ClosePrice { get; set; }
            public double? OpenPrice { get; set; }
            public double? HighPrice { get; set; }
            public double? LowPrice { get; set; }
            public long volume { get; set; }
            public long Capitalization { get; set; }
        }
        public class BoraItem
        {
            public DateTime Date { get; set; }
            public int Code { get; set; }
            public string Name { get; set; }
            public double? BoraAverage { get; set; }
            public double? BoraPercentAverage { get; set; }
            public double? ClosePrice { get; set; }
            public double? OpenPrice { get; set; }
            public double? HighPrice { get; set; }
            public double? LowPrice { get; set; }
            public long volume { get; set; }
            public long Capitalization { get; set; }
        }
    }
}
