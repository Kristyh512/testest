using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using static Project.Program;

namespace Project
{
    internal class Program
    {
        public enum PriceMode
        {
            Close,
            Low, 
            High,
            Open,
            AdjClose
        }

        public enum Side
        {
            Long, 
            Short
        }

        public class StockData
        {
            private DateTime _date;
            private double _open, _high, _low, _close, _adjClose, _volume;
            public StockData(DateTime date, double open, double high, double low, double close, double adjClose, double volume) 
            {   
                _date = date;
                _open= open;
                _high= high;
                _low= low;
                _close= close;
                _adjClose= adjClose;
                _volume= volume;
            }
            public DateTime DateTime => _date;
            public double Open => _open;
            public double High => _high;
            public double Low => _low;
            public double Close => _close;
            public double Volume => _volume;
            public double AdjClose => _adjClose;
        }

        public class Asset
        {
            private string _name;
            private double _price;
            private int _volume;

            public Asset(string name, double price, int volume) 
            {
                _name = name;
                _price = price;
                _volume = volume;
            }

            public string Name => _name;
            public double Price => _price;
            public int Volume => _volume;

            public double GetPrice()
            {
                return _price * _volume;
            }

            public void UpdataPrice(double price)
            {
                _price = price;
            }
        }

        public class Portfolio
        {
            private double _pnL;
            private List<Asset> _assets; 

            public Portfolio()
            {
                _assets = new List<Asset>();
            }

            public double PnL => _pnL;
            public List<Asset> Assets => _assets;

            public void UpdateAssetPrice(Dictionary<string, double> prices)
            {
                foreach (Asset asset in _assets)
                {
                    double newPrice = prices[asset.Name];
                    asset.UpdataPrice(newPrice);
                }
                UpdatePnL();
            }

            public void Add(Asset asset)
            {
                _assets.Add(asset);
                UpdatePnL();
            }
            public bool RemoveAsset(string assetName)
            {
                Asset asset = _assets.Find(a => a.Name == assetName);
                if (asset != null)
                {
                    _assets.Remove(asset);
                    UpdatePnL();
                    return true;
                }
                return false;
            }

            private void UpdatePnL()
            {
                _pnL = 0;
                foreach (Asset asset in _assets)
                    _pnL += asset.GetPrice();
            }
        }


        static List<double> CalculateMovingAverage(List<double> data, int windowSize)
        {
            var movingAverages = new List<double>();

            for (int i = 0; i <= data.Count - windowSize; i++)
            {
                double sum = 0;
                for (int j = i; j < i + windowSize; j++)
                {
                    sum += data[j];
                }
                movingAverages.Add(sum / windowSize);
            }

            return movingAverages;
        }

        static List<double> CalculatePriceReturns(List<double> data, int lag, int timePosition, bool backward = true)
        {
            List<double> result = new List<double>();
            if (!backward)
            {
                int k = timePosition + lag;
                while (k < data.Count)
                {
                    result.Add((data[k] / data[k - lag]) - 1);
                    k = k + lag;
                }
                int lastIndex = k - lag;
                result.Add((data[data.Count-1] / data[lastIndex] - 1));
            }
            else
            {
                int k = timePosition;
                while (k > 0)
                {
                    result.Add((data[k] / data[k - lag]) - 1);
                    k = k - lag;
                }
                int lastIndex = k + lag;
                result.Add((data[0] / data[lastIndex] - 1));
            }
            return result;
        }

        static List<StockData> GetData(string path)
        {
            using (var reader = new StreamReader(path))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            }))
            {
                var records = new List<StockData>();
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var record = new StockData
                        (Convert.ToDateTime(csv.GetField("Date")),
                         (double)csv.GetField<decimal>("Open"),
                         (double)csv.GetField<decimal>("High"),
                         (double)csv.GetField<decimal>("Low"),
                         (double)csv.GetField<decimal>("Close"),
                         (double)csv.GetField<decimal>("Adj Close"),
                         (double)csv.GetField<long>("Volume"));
                    records.Add(record);
                }
                return records;
            }
        }

        static void BuyAsset(double currentPrice,Portfolio portfolio)
        {
            int volume = (int)(portfolio.PnL / currentPrice);
            Asset asset = new Asset("AMZN", currentPrice,volume);
            portfolio.Add(asset);
        }

        static Dictionary<string, double> GenerateNewPrice(double price)
        {
            return new Dictionary<string, double> ()
            {
                { "AMZN",price }
            };
        }
                
        static double BackTesting(List<double> movingAverage, List<StockData> StockData, int volume, PriceMode priceMode, double initialCash)
        {
            double cash = initialCash;
            if (movingAverage.Count != StockData.Count) throw new InvalidOperationException($"Unconsistency among datas :\nMoving Average Size = {movingAverage.Count} \nStock Data Size = {StockData.Count} ");
            List<double> data = StockData.Select(x => x.Close).ToList();

            Portfolio portfolio = new Portfolio();

            double movingAverage0 = movingAverage[0],
                movingAverage1 = movingAverage[1],
                data0 = data[0],
                data1 = data[1],
                spread0 = movingAverage0 - data0,
                spread1 = movingAverage1 - data1;

            int k = 0;
            while (spread0 < 0)
            {
                k++;
                spread0 = movingAverage[k] - data[k];
            }

            BuyAsset(data[k], portfolio);
            cash = initialCash - portfolio.PnL;
            Side side = Side.Long;
            
            for (int i = k; i < movingAverage.Count; i++)
            {
                movingAverage0 = movingAverage[i];
                movingAverage1 = movingAverage[i+1];
                data0 = data[i];
                data1 = data[i+1];
                spread0 = movingAverage0 - data0;
                spread1 = movingAverage1 - data1;

                //mise 
                Dictionary<string, double> newPrices = GenerateNewPrice(data1);
                portfolio.UpdateAssetPrice(newPrices);

                if (spread0 * spread1 < 0)
                {
                    if(spread0 > 0 && spread1 < 0)
                    {
                        side = Side.Short;
                        //Mettre à jour la valeur du pnL
                        
                        //vente d'actions
                        cash = cash + portfolio.Assets[0].GetPrice();
                        portfolio.RemoveAsset("AMZN");
                    }
                    else
                    {
                        side = Side.Long;
                        //Mettre à jour la valeur du pnL
                        Dictionary<string, double> newPrices = GenerateNewPrice(data1);
                        portfolio.UpdateAssetPrice(newPrices);
                        //achat d'actions
                        BuyAsset(data[k], portfolio);
                        cash = cash - portfolio.Assets[0].GetPrice();
                        portfolio.RemoveAsset("AMZN");
                    }
                }
            }

        }
        static void Main(string[] args)
        {
            string path = "C:\\Users\\Kristy\\Desktop\\Project\\bin\\Debug\\AMZN.csv";
            List<StockData> stockDatas = GetData(path);
            //attention point de départ cohérence 
            List<double> returnsBackward = CalculatePriceReturns(stockDatas.Select(data => data.Close).ToList(), 1, 0, false);

            List<double> MA5 = CalculateMovingAverage(stockDatas.Select(data => data.Close).ToList(), 10);



            Console.WriteLine(path);

        }
    }
}
