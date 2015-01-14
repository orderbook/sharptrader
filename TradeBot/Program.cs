using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

// taken from http://fastjson.codeplex.com/ LGPL 2.1
using fastJSON;

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
