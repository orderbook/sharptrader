using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

using WebSockets.Net;
using WebSockets.Utils;

using Newtonsoft.Json;

namespace MarketMaker.Trades
{
    public class OKCoin : IDisposable, IWebSocketHandler
    {
        private string wsurl = "wss://real.okcoin.com:10440/websocket/okcoinapi";
        private string restURL = "https://www.okcoin.com/api/v1/";
        WebSocket ws;

        private string apiKey = "";
        private string apiSecret = "";

        public OKCoin(string apiKey_, string apiSecret_)
        {
            apiKey = apiKey_;
            apiSecret = apiSecret_;
        }

        public void Connect()
        {
            ws = new WebSocket();
            ws.Handler = this;
            //ws.Open(wsurl);

            // Subscribe to channels
            //ws.Send("{'event':'ping'}");
            //ws.SendUTF8("{'event':'addChannel','channel':'ok_btcusd_ticker'}");
            //ws.SendAscii("{'event':'addChannel','channel':'ok_btcusd_trades'}");
            //ws.SendAscii("{'event':'addChannel','channel':'ok_btcusd_depth'}");
        }

        public void Close()
        {
            if (ws != null)
            {
                ws.Handler = null;
                ws.Close();
            }
        }

        public void Dispose()
        {
            Close();
        }

        // REST api
        public OkcDepth getDepthFutures(OKCoinFuturesType ftype)
        {
            int size = 200;
            int merge = 1;
            try
            {
                string url = (this.restURL) + "future_depth.do";
                string postData = string.Format("symbol=btc_usd&contract_type={0}&size={1}&merge={2}", getFuturesTypeString(ftype), size, merge);
                //string responseStr = DoAuthenticatedAPIPost(url, apiKey, apiSecret, postData);
                string result = MarketMaker.HttpGet(url, postData);

                var obj = JsonConvert.DeserializeObject<OkcDepth>(result);

                return obj;

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public OkcTickerMsg getFuturesTicker(OKCoinFuturesType ftype)
        {
            try
            {
                string url = (this.restURL) + "future_ticker.do";
                string postData = string.Format("symbol=btc_usd&contract_type={0}", getFuturesTypeString(ftype));
                string result = MarketMaker.HttpGet(url, postData);

                var obj = JsonConvert.DeserializeObject<OkcTickerMsg>(result);

                return obj;

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public OkcFuturesHoldingCross getFuturesPositionsCross()
        {
            try
            {
                string url = (this.restURL) + "future_position.do";
                string postData = string.Format("api_key={0}&symbol=btc_usd", apiKey);

                string signString = postData + "&secret_key=" + apiSecret;
                string signature = CalculateMD5Hash(signString);

                postData += "&sign=" + signature;

                string result = MarketMaker.HttpPost(url, postData);

                //var obj = JsonConvert.DeserializeObject<OkcTickerMsg>(result);

                return null;

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public OkcFuturesHoldingFix getFuturesPositionsFix()
        {
            try
            {
                string url = (this.restURL) + "future_position_4fix.do";
                string postData = string.Format("api_key={0}&symbol=btc_usd&type=1", apiKey);

                string signString = postData + "&secret_key=" + apiSecret;
                string signature = CalculateMD5Hash(signString);

                postData += "&sign=" + signature;

                string result = MarketMaker.HttpPost(url, postData);

                var obj = JsonConvert.DeserializeObject<OkcFuturesHoldingFix>(result);

                return obj;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public OkcFuturesUserInfoFix getFuturesUserInfoFix()
        {
            try
            {
                string url = (this.restURL) + "future_userinfo_4fix.do";
                string postData = string.Format("api_key={0}", apiKey);

                string signString = postData + "&secret_key=" + apiSecret;
                string signature = CalculateMD5Hash(signString);

                postData += "&sign=" + signature;

                string result = MarketMaker.HttpPost(url, postData);

                var obj = JsonConvert.DeserializeObject<OkcFuturesUserInfoFix>(result);

                return obj;
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        private string getFuturesTypeString(OKCoinFuturesType ftype)
        {
            switch (ftype)
            {
                case OKCoinFuturesType.ThisWeek:
                    return "this_week";

                case OKCoinFuturesType.NextWeek:
                    return "next_week";

                case OKCoinFuturesType.Quarter:
                    return "quarter";

                default:
                    return null;
            }
        }

        private string CalculateMD5Hash(string input)
        {
            // step 1, calculate MD5 hash from input
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }

        #region IWebSocketHandler
        void IWebSocketHandler.OnString(WebSocket sender, string data)
        {
            //handleString(data);
        }

        void IWebSocketHandler.OnBinary(WebSocket sender, byte[] data)
        {
        }

        void IWebSocketHandler.OnPong(WebSocket sender, byte[] data)
        {
        }

        void IWebSocketHandler.OnError(WebSocket sender, string op, Exception err)
        {
            /*var cback = OnError;
            if (cback != null)
                try { cback(null, err.Message, op); }
                catch (Exception err2) { Helper.RaiseUnexpected(err2); }*/
        }

        void IWebSocketHandler.OnShutdown(WebSocket sender, ShutdownCode code, byte[] data)
        {
            // ignore ?
        }

        volatile bool _wasDisconnect = false;
        void IWebSocketHandler.OnClosed(WebSocket sender)
        {
            if (_wasDisconnect)
                return;
            /*
            var cback = OnDisconnect;
            if (cback != null)
                try { cback(null); }
                catch (Exception err) { Helper.RaiseUnexpected(err); }*/
            _wasDisconnect = true;
        }
        #endregion IWebSocketHandler

    }
}
