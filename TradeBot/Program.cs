using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

using WebSockets.Net;
using WebSockets.Utils;

using MarketMaker.BistampTypes;

namespace MarketMaker.Trades
{
    class Program
    {
        public static void Main(string[] args)
        {
            MarketMaker mm = new MarketMaker("config.json");

            Console.WriteLine("\nPress [ENTER] for exit\n\n");
            Console.ReadLine();
        }
    }
}
