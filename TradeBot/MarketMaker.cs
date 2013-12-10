/*
 * Program: Trading bot
 * Author: ICBIT team (https://icbit.se)
 * License: GPLv3 by Free Software Foundation
 * Comments: Core arbitrage trading logic implementation
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.IO;
using System.Net;
using System.Security.Cryptography;

// taken from http://fastjson.codeplex.com/ LGPL 2.1
using fastJSON;

using WebSockets.Net;
using WebSockets.Utils;

using MarketMaker.MtGoxTypes;


namespace MarketMaker.Trades
{
    [Serializable]
    public class IcbitConfig
    {
        public uint UserId { get; set; }
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }
    }

    [Serializable]
    public class MtGoxConfig
    {
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }
    }

    [Serializable]
    public class MarketMakerConfig
    {
        public IcbitConfig icbit { get; set; }
        public MtGoxConfig mtgox { get; set; }
    }

    // Signature is compatible between Bitstamp and ICBIT
    public class Authenticator
    {
        /*public void AddApiAuthentication(RestRequest restRequest)
        {
            var nonce = DateTime.Now.Ticks;
            var signature = GetSignature(nonce, apiKey, apiSecret, clientId);

            restRequest.AddParameter("key", apiKey);
            restRequest.AddParameter("signature", signature);
            restRequest.AddParameter("nonce", nonce);
        }*/

        public static long GetUnixTimeStamp()
        {
            DateTime time = DateTime.UtcNow;
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

            // unix is number of microseconds since 1 Jan 1970
            TimeSpan epochTimespan = time.Subtract(dtDateTime);

            // ticks per microsecond = 10.
            long result = epochTimespan.Ticks / 10000000;
            return result;
        }

        public static string GetSignature(long nonce, string key, string secret, string clientId)
        {
            string msg = string.Format("{0}{1}{2}", nonce, clientId, key);

            return ByteArrayToString(SignHMACSHA256(secret, Encoding.ASCII.GetBytes(msg))).ToUpper();
        }

        private static byte[] SignHMACSHA256(String key, byte[] data)
        {
            HMACSHA256 hashMaker = new HMACSHA256(Encoding.ASCII.GetBytes(key));
            return hashMaker.ComputeHash(data);
        }

        private static string ByteArrayToString(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "");
        }
    }

    public class MarketMaker
    {
        SocketIO sock;
        Icbit icbit;

        MarketMakerConfig config;

        public MarketMaker(string configFilename)
        {
            // Read config file
            try
            {
                using (StreamReader sr = new StreamReader(configFilename))
                {
                    config = JSON.Instance.ToObject<MarketMakerConfig>(sr.ReadToEnd());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception while reading config file: " + e.ToString());
                return;
            }

            // Create ICBIT exchange object
            icbit = new Icbit(config.icbit.UserId, config.icbit.ApiKey, config.icbit.ApiSecret);
            icbit.OrdersChanged += new EventHandler(IcbitOrdersChanged);
            icbit.BalanceChanged += new EventHandler(IcbitBalanceChanged);
            icbit.ConnectEvent += new EventHandler(IcbitConnected);

            // Connect to the ICBIT exchange
            icbit.Connect();
        }

        public void IcbitBalanceChanged(object sender, EventArgs e)
        {
            Console.WriteLine("ICBIT: Balance updated!");
        }

        public void IcbitOrdersChanged(object sender, EventArgs e)
        {
            Console.WriteLine("ICBIT: Orders updated!");
        }

        public void IcbitConnected(object sender, EventArgs e)
        {
            Console.WriteLine("ICBIT: Connected and ready to work!");
            //icbit.SubscribeToChannel("orderbook_BUZ3");
        }

        void StartMtGoxStreaming()
        {
            sock = new SocketIO();
            sock.OnMessage += onMessage;
            sock.OnJson += onJson;
            sock.Open(@"https://socketio.mtgox.com/", Encoding.ASCII);

            // choose /mtgox namespace
            sock.WebSocket.SendAscii("1::/mtgox");

            //sock.WebSocket.SendAscii( "4::/mtgox:{\"op\":\"mtgox.subscribe\",\"type\":\"ticker\"}" );
            sock.WebSocket.SendAscii("4::/mtgox:{\"op\":\"unsubscribe\",\"channel\":\"dbf1dee9-4f2e-4a08-8cb7-748919a71b21\"}"); // trades
            sock.WebSocket.SendAscii("4::/mtgox:{\"op\":\"unsubscribe\",\"channel\":\"24e67e0d-1cad-4cc0-9e7a-f8523ef460fe\"}"); // depth

            //01:45:21.511 DEBUG: DATA: 4::/mtgox:{"op":"subscribe","channel":"dbf1dee9-4f2e-4a08-8cb7-748919a71b21"}
            //01:45:21.585 DEBUG: DATA: 4::/mtgox:{"op":"subscribe","channel":"d5f06780-30a8-4a48-a2f8-7ed181b4a13f"}
            //01:45:21.587 DEBUG: DATA: 4::/mtgox:{"op":"subscribe","channel":"24e67e0d-1cad-4cc0-9e7a-f8523ef460fe"}
        }

        void onMessage(string msg, int? msgId, string ep)
        {
            //Console.WriteLine("MESSAGE: " + msg);
        }

        void onJson(string json, int? msgId, string ep)
        {
            var obj = JSON.Instance.ToObject<Packet>(json);
            //Console.WriteLine("JSON: " + obj);

            // Unsubscribe from unneeded channels if we got subscribed to them automatically
            if (obj.op == OpType.subscribe && obj.channel != "d5f06780-30a8-4a48-a2f8-7ed181b4a13f")
            {
                sock.WebSocket.SendAscii("4::/mtgox:{\"op\":\"unsubscribe\",\"channel\":\"" + obj.channel + "\"}");
            }

            // Handle ticker
            if (obj.op == OpType.@private && obj.@private == PacketContent.ticker)
            {
                //04:11:17.084 DEBUG: DATA: 4::/mtgox:{"channel":"d5f06780-30a8-4a48-a2f8-7ed181b4a13f","op":"private","origin":"broadcast","private":"ticker","ticker":{"high":{"value":"5.92000","value_int":"592000","display":"$5.92000","display_short":"$5.92","currency":"USD"},"low":{"value":"5.53000","value_int":"553000","display":"$5.53000","display_short":"$5.53","currency":"USD"},"avg":{"value":"5.67060","value_int":"567060","display":"$5.67060","display_short":"$5.67","currency":"USD"},"vwap":{"value":"5.66442","value_int":"566442","display":"$5.66442","display_short":"$5.66","currency":"USD"},"vol":{"value":"86111.67583170","value_int":"8611167583170","display":"86,111.67583170??BTC","display_short":"86,111.68??BTC","currency":"BTC"},"last_local":{"value":"5.92000","value_int":"592000","display":"$5.92000","display_short":"$5.92","currency":"USD"},"last":{"value":"5.92000","value_int":"592000","display":"$5.92000","display_short":"$5.92","currency":"USD"},"last_orig":{"value":"5.92000","value_int":"592000","display":"$5.92000","display_short":"$5.92","currency":"USD"},"last_all":{"value":"5.92000","value_int":"592000","display":"$5.92000","display_short":"$5.92","currency":"USD"},"buy":{"value":"5.90010","value_int":"590010","display":"$5.90010","display_short":"$5.90","currency":"USD"},"sell":{"value":"5.92000","value_int":"592000","display":"$5.92000","display_short":"$5.92","currency":"USD"}}}

                var ticker = obj.ticker;

                // Update ICBIT's orders accordingly
                //UpdateByTicker(ticker.buy.value_int, ticker.sell.value_int);
            }
        }

        public static string HttpPost(string uri, string parameters)
        {
            bool requestFailed = false;

            // parameters: name1=value1&name2=value2	
            //System.Net.ServicePointManager.CertificatePolicy = new MyPolicy();
            ServicePointManager.MaxServicePointIdleTime = 200;
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);
            //webRequest.UserAgent = "Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US) AppleWebKit/534.21 (KHTML, like Gecko) Chrome/11.0.682.0 Safari/534.21";
            webRequest.UserAgent = "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 5.1)";

            //string ProxyString = 
            //   System.Configuration.ConfigurationManager.AppSettings
            //   [GetConfigKey("proxy")];
            //webRequest.Proxy = new WebProxy (ProxyString, true);
            //Commenting out above required change to App.Config
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.Method = "POST";
            byte[] bytes = Encoding.ASCII.GetBytes(parameters);
            Stream os = null;
            try
            { // send the Post
                webRequest.ContentLength = bytes.Length;   //Count bytes to send
                os = webRequest.GetRequestStream();
                os.Write(bytes, 0, bytes.Length);         //Send it
            }
            catch (WebException ex)
            {
                //MessageBox.Show(ex.Message, "HttpPost: Request error",
                //MessageBoxButtons.OK, MessageBoxIcon.Error);
                requestFailed = true;
            }
            finally
            {
                if (os != null)
                {
                    os.Close();
                }
            }

            if (!requestFailed)
            {
                try
                { // get the response
                    WebResponse webResponse = webRequest.GetResponse();
                    if (webResponse == null)
                    { return null; }
                    StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                    return sr.ReadToEnd().Trim();
                }
                catch (WebException ex)
                {
                    // MessageBox.Show(ex.Message, "HttpPost: Response error",
                    //   MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return null;
        }
    }
}
