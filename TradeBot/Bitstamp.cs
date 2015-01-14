/*
 * Program: Arbitrage trading bot
 * Author: ICBIT team (https://icbit.se)
 * License: GPLv3 by Free Software Foundation
 * Comments: Class providing interface to the ICBIT trading API
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using MarketMaker.BistampTypes;
using fastJSON;

namespace MarketMaker.Trades
{
    public class Bitstamp
    {
        public string userId;
        public string apiKey;
        public string apiSecret;

        string baseURL = "https://www.bitstamp.net/api/";
        string UserAgent = "TAYPE International/0.1 MtGox API Client";

        public Bitstamp()
        {
        }

#if notimplemented

        /// <summary>
        /// 0/data/getTrades.php 
        /// </summary>
        public List<Trade> getTrades(string sinceTradeID)
        {
            try
            {
                string url = (this.baseURL) + "0/data/getTrades.php";
                string postData = "";
                if (sinceTradeID != "")
                    url += "?since=" + sinceTradeID;
                string responseStr = DoAuthenticatedAPIPost(url, apiKey, apiSecret, postData);
                return null;//Trade.getObjects(responseStr);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 0/data/getDepth.php 
        /// </summary>
        public Depth getDepth(Currency currency)
        {
            try
            {
                string url = (this.baseURL) + "0/data/getDepth.php?currency=" + currency.ToString();
                string postData = "";
                string responseStr = DoAuthenticatedAPIPost(url, apiKey, apiSecret, postData);

                Depth returnValue = new Depth();//Depth.getObjects(responseStr);

                return returnValue;

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 0/getFunds.php 
        /// </summary>
        public double getFunds()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 0/buyBTC.php 
        /// </summary>
        public List<Order> buyBTC(double amount, Currency currency, double price = 0.0)
        {
            try
            {
                string url = (this.baseURL) + "0/buyBTC.php";
                string postData;
                if (price == 0.0)
                    postData = "amount=" + amount + "&currency=" + currency.ToString();
                else
                    postData = "amount=" + amount + "&price=" + price + "&currency=" + currency.ToString();
                string responseStr = DoAuthenticatedAPIPost(url, apiKey, apiSecret, postData);
                var resp = JSON.Instance.ToObject<OrderResponse>(responseStr);
                return resp.orders;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 0/sellBTC.php 
        /// </summary>
        public List<Order> sellBTC(double amount, Currency currency, double price = 0.0)
        {
            try
            {
                string url = (this.baseURL) + "0/sellBTC.php";
                string postData;
                if (price == 0.0)
                    postData = "amount=" + amount + "&currency=" + currency.ToString();
                else
                    postData = "amount=" + amount + "&price=" + price + "&currency=" + currency.ToString();
                string responseStr = DoAuthenticatedAPIPost(url, apiKey, apiSecret, postData);
                var resp = JSON.Instance.ToObject<OrderResponse>(responseStr);
                return resp.orders;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 0/getOrders.php 
        /// </summary>
        public List<Order> getOrders(OrderDirection type, OrderStatus status, int oid = 0)
        {
            try
            {
                string url = (this.baseURL) + "0/getOrders.php";
                string postData = "";
                postData = "oid=" + oid;
                switch (type)
                {
                    case OrderDirection.bid:
                        postData += "&type=1";
                        break;
                    case OrderDirection.ask:
                        postData += "&type=2";
                        break;
                }
                switch (status)
                {
                    case OrderStatus.Active:
                        postData += "&status=1";
                        break;
                    case OrderStatus.NotSufficientFunds:
                        postData += "&status=2";
                        break;
                }
                string responseStr = DoAuthenticatedAPIPost(url, apiKey, apiSecret, postData);

                /*
                {  "usds":"0",
                 * "btcs":"0",
                 * "new_usds":{
                 *      "value":"0.00000",
                 *      "value_int":"0",
                 *      "display":"$0.00000",
                 *      "display_short":"$0.00",
                 *      "currency":"USD"},
                 *  "new_btcs":{
                 *      "value":"0.00000000",
                 *      "value_int":"0",
                 *      "display":"0.00000000\u00a0BTC",
                 *      "display_short":"0.00\u00a0BTC",
                 *      "currency":"BTC"},
                 *   "orders":[{"oid":"07dfa77e-eedb-4d68-bc77-55280a909cda","currency":"USD","item":"BTC","type":2,"amount":"1.00000000","amount_int":"100000000","price":"6.19990","price_int":"619990","status":0,"real_status":"invalid","dark":0,"date":1325782840,"priority":"1325782840517162"},{"oid":"205fc95a-89f0-4ebb-becf-43032deca20d","currency":"USD","item":"BTC","type":2,"amount":"1.00000000","amount_int":"100000000","price":"5.53808","price_int":"553808","status":0,"real_status":"invalid","dark":0,"date":1325712373,"priority":"1325712373261098"},{"oid":"429d5929-dba1-4f95-af68-dd3b131e5956","currency":"USD","item":"BTC","type":2,"amount":"2.00000000","amount_int":"200000000","price":"6.70970","price_int":"670970","status":0,"real_status":"invalid","dark":0,"date":1325886640,"priority":"1325886640581576"},{"oid":"5897bfd6-6fdf-4e03-80d8-75ce254186f8","currency":"USD","item":"BTC","type":2,"amount":"1000.00000000","amount_int":"100000000000","price":"10.00000","price_int":"1000000","status":0,"real_status":"invalid","dark":0,"date":1344979233,"priority":"1344979233573133"},{"oid":"65bc20e6-0ec7-48be-81e3-b5a8a59fc86f","currency":"USD","item":"BTC","type":2,"amount":"0.29860000","amount_int":"29860000","price":"6.63000","price_int":"663000","status":0,"real_status":"invalid","dark":0,"date":1325885130,"priority":"1325885130360685"},{"oid":"7a10f3c0-dab3-4817-9a32-b6f3f2b9d0d2","currency":"USD","item":"BTC","type":2,"amount":"1000.00000000","amount_int":"100000000000","price":"10.00000","price_int":"1000000","status":0,"real_status":"invalid","dark":0,"date":1344981405,"priority":"1344981405037818"},{"oid":"b87ceee1-04c5-4d91-a94e-54ee1d221411","currency":"USD","item":"BTC","type":2,"amount":"0.14900000","amount_int":"14900000","price":"6.70970","price_int":"670970","status":0,"real_status":"invalid","dark":0,"date":1325886436,"priority":"1325886436583107"},{"oid":"c533db7c-c972-429e-8084-ef0bbb09c1c9","currency":"USD","item":"BTC","type":2,"amount":"0.29860000","amount_int":"29860000","price":"6.63000","price_int":"663000","status":0,"real_status":"invalid","dark":0,"date":1325885128,"priority":"1325885128171198"},{"oid":"df55f311-f675-4b11-8fcc-f2a8061d21bb","currency":"USD","item":"BTC","type":2,"amount":"1000.00000000","amount_int":"100000000000","price":"10.00000","price_int":"1000000","status":0,"real_status":"invalid","dark":0,"date":1344979257,"priority":"1344979257804781"},{"oid":"e1e40c79-aeef-4f0c-affc-1e415cfb402c","currency":"USD","item":"BTC","type":2,"amount":"1000.00000000","amount_int":"100000000000","price":"10.00000","price_int":"1000000","status":0,"real_status":"invalid","dark":0,"date":1344979054,"priority":"1344979054815263"},{"oid":"f597c8fa-2a17-4db0-9695-71a2394c748f","currency":"USD","item":"BTC","type":2,"amount":"1.00000000","amount_int":"100000000","price":"6.19990","price_int":"619990","status":0,"real_status":"invalid","dark":0,"date":1325782848,"priority":"1325782848803275"},{"oid":"f92614b2-ecc7-470f-9f36-01bb19c65796","currency":"USD","item":"BTC","type":2,"amount":"0.29860000","amount_int":"29860000","price":"6.63000","price_int":"663000","status":0,"real_status":"invalid","dark":0,"date":1325885120,"priority":"1325885120229746"}]}
                 */
                var resp = JSON.Instance.ToObject<OrderResponseWithBalance>(responseStr);
                return resp.orders;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 0/cancelOrder.php 
        /// </summary>
        public List<Order> cancelOrder(string oid, OrderDirection type)
        {
            try
            {
                string url = (this.baseURL) + "0/cancelOrder.php";
                string postData;
                int t = 0;
                switch (type)
                {
                    case OrderDirection.bid:
                        t = 2;
                        break;
                    case OrderDirection.ask:
                        t = 1;
                        break;
                }
                postData = "oid=" + oid + "&type=" + t;
                string responseStr = DoAuthenticatedAPIPost(url, apiKey, apiSecret, postData);
                var resp = JSON.Instance.ToObject<OrderResponseWithBalance>(responseStr);
                return resp.orders;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 0/history_[CUR].csv
        /// </summary>
        public string history_CUR(Currency currency)
        {
            try
            {
                string url = (this.baseURL) + "0/history_" + currency.ToString() + ".csv";
                string postData = "";
                string responseStr = DoAuthenticatedAPIPost(url, apiKey, apiSecret, postData);
                return responseStr;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 0/info.php
        /// </summary>
        public HistoryItem info()
        {
            try
            {
                string url = (this.baseURL) + "0/info.php";
                string postData = "";
                string responseStr = DoAuthenticatedAPIPost(url, apiKey, apiSecret, postData);
                return JSON.Instance.ToObject<HistoryItem>(responseStr);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Perform an authenticated post to MtGox client API
        /// </summary>        
        string DoAuthenticatedAPIPost(string url, string apiKey, string apiSecret, string moreargs = null)
        {
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
                throw new ArgumentException("Cannot call private api's without api key and secret");
            var parameters = "nonce=" + DateTime.Now.Ticks.ToString();
            if (!string.IsNullOrEmpty(moreargs))
                parameters += "&" + moreargs;
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.Method = "POST";
            webRequest.UserAgent = UserAgent;
            webRequest.Accept = "application/json";
            webRequest.Headers["Rest-Key"] = apiKey;
            webRequest.Headers["Rest-Sign"] = EncodeParamsToSecret(parameters, apiSecret);
            byte[] byteArray = Encoding.UTF8.GetBytes(parameters);
            webRequest.ContentLength = byteArray.Length;
            using (Stream dataStream = webRequest.GetRequestStream())
            {
                dataStream.Write(byteArray, 0, byteArray.Length);
            }
            using (WebResponse webResponse = webRequest.GetResponse())
            {
                using (Stream str = webResponse.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(str))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
        }

        /// <summary>
        /// Get HMACSHA512 hash of specified post parameters using the apiSecret as the key
        /// </summary>       
        string EncodeParamsToSecret(string parameters, string apiSecret)
        {
            var hmacsha512 = new HMACSHA512(Convert.FromBase64String(apiSecret));
            var byteArray = hmacsha512.ComputeHash(Encoding.UTF8.GetBytes(parameters));
            return Convert.ToBase64String(byteArray);
        }
#endif
    }
}
