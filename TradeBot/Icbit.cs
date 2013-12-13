/*
 * Program: Trading bot
 * Author: ICBIT team (https://icbit.se)
 * License: GPLv3 by Free Software Foundation
 * Comments: Class providing interface to the ICBIT trading API
 */

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;

using fastJSON;

using WebSockets.Net;
using WebSockets.Utils;

using MarketMaker.IcbitTypes;

namespace MarketMaker.Trades
{
    public class Icbit
    {
        string ConnUrl;
        SocketIO sock;

        public ConcurrentDictionary<string, Instrument>[] instruments;
        public ConcurrentDictionary<long, Order>[] orders;
        public ConcurrentDictionary<string, Position> balance;

        private static AutoResetEvent eventOrderUpdated;
        private Boolean connected;

        // Events
        public event EventHandler OrdersChanged;
        public event EventHandler BalanceChanged;
        public event EventHandler ConnectEvent;

        public Icbit(uint userid, string apiKey, string apiSecret)
        {
            orders = new ConcurrentDictionary<long, Order>[2];
            orders[0] = new ConcurrentDictionary<long, Order>();
            orders[1] = new ConcurrentDictionary<long, Order>();
            balance = new ConcurrentDictionary<string, Position>();
            instruments = new ConcurrentDictionary<string, Instrument>[2];
            instruments[0] = new ConcurrentDictionary<string, Instrument>();
            instruments[1] = new ConcurrentDictionary<string, Instrument>();
            eventOrderUpdated = new AutoResetEvent(false);
            connected = false;

            long nonce = Authenticator.GetUnixTimeStamp();
            string signature = Authenticator.GetSignature(nonce, apiKey, apiSecret, userid.ToString());

            ConnUrl = String.Format("https://api.icbit.se:443/?key={0}&signature={1}&nonce={2}", apiKey, signature, nonce);

            sock = new SocketIO();
            sock.OnMessage += onMessage;
            sock.OnJson += onJson;
        }

        public void Connect()
        {
            // Open the socket
            sock.Open(ConnUrl, Encoding.ASCII);

            // And choose /icbit namespace
            sock.WebSocket.SendAscii("1::/icbit");
        }

        public void CreateOrderCurrency(uint ticker, Boolean buy, long price, long qty)
        {
            string buyStr = buy ? "1" : "0";

            string tradeStr = "4::/icbit:{\"op\":\"create_order\",\"order\":{\"market\":0,\"ticker\":\"" + ticker + "\",\"buy\":" + buyStr + ",\"price\":" + price + ",\"qty\":" + qty + "}}";

            // Reset previous signals of the order-received event
            eventOrderUpdated.Reset();

            // Send the command
            //Console.WriteLine("[{0}] Creating new order", DateTime.Now.ToString("HH:mm:ss.fff"));
            sock.WebSocket.SendAscii(tradeStr);

            // Wait till it's processed
            if (!eventOrderUpdated.WaitOne(new TimeSpan(0, 0, 10)))
            {
                // Timeout....
                // TODO: Add handling of this situation
            }
        }

        public void CreateOrderFutures(string ticker, Boolean buy, long price, long qty)
        {
            string buyStr = buy ? "1" : "0";

            string tradeStr = "4::/icbit:{\"op\":\"create_order\",\"order\":{\"market\":1,\"ticker\":\"" + ticker + "\",\"buy\":" + buyStr + ",\"price\":" + price + ",\"qty\":" + qty + "}}";

            // Reset previous signals of the order-received event
            eventOrderUpdated.Reset();

            // Send the command
            //Console.WriteLine("[{0}] Creating new order", DateTime.Now.ToString("HH:mm:ss.fff"));
            sock.WebSocket.SendAscii(tradeStr);

            // Wait till it's processed
            if (!eventOrderUpdated.WaitOne(new TimeSpan(0, 0, 10)))
            {
                // Timeout....
                // TODO: Add handling of this situation
            }
        }

        public void CancelOrder(int marketType, long oid)
        {
            string tradeStr = "4::/icbit:{\"op\":\"cancel_order\",\"order\":{\"oid\":" + oid + ",\"market\":" + marketType + "}}";

            // Reset previous signals of the order-received event
            eventOrderUpdated.Reset();

            // Send the command
            //Console.WriteLine("[{0}] Sending cancel oid {1}", DateTime.Now.ToString("HH:mm:ss.fff"), oid);
            sock.WebSocket.SendAscii(tradeStr);

            // Wait till it's processed
            if (!eventOrderUpdated.WaitOne(new TimeSpan(0, 0, 10)))
            {
                // Timeout....
                // TODO: Add handling of this situation
            }
        }

        public void ClearAllOrders()
        {
            foreach (var o in orders[MarketType.Futures].Keys)
            {
                CancelOrder(MarketType.Futures, o);
            }

            foreach (var o in orders[MarketType.Currency].Keys)
            {
                CancelOrder(MarketType.Currency, o);
            }
        }

        public void SubscribeToChannel(string channel)
        {
            string subsStr = "4::/icbit:{\"op\":\"subscribe\",\"channel\": \""+channel+"\"}";

            // Send the command
            sock.WebSocket.SendAscii(subsStr);
        }

        public void GetTrades(long since, uint limit)
        {
            string subsStr = String.Format("4::/icbit:{{\"since\":\"{0}\",\"limit\":{1},\"type\":\"user_trades\",\"op\":\"get\"}}", since, limit);

            // Send the command
            sock.WebSocket.SendAscii(subsStr);
        }

        void onMessage(string msg, int? msgId, string ep)
        {
            //Console.WriteLine("MESSAGE: " + msg);
        }

        void onJson(string json, int? msgId, string ep)
        {
            var obj = JSON.Instance.ToObject<Packet>(json);
            //Console.WriteLine("JSON: " + obj);

            // Handle incoming messages
            if (obj.op == OpType.@private)
            {
                // Handle user balance
                if (obj.op == OpType.@private && obj.@private == PacketContent.user_balance)
                {
                    var v = obj.user_balance;

                    // Clear all balance
                    balance.Clear();

                    // Add all to the local cache
                    foreach (var p in v)
                        balance.TryAdd(p.ticker, p);

                    // Raise the balance changed event
                    OnBalanceChanged(new EventArgs());
                }

                // Handle user orders
                if (obj.op == OpType.@private && obj.@private == PacketContent.user_order)
                {
                    // If it's not an update - purge existing orders list
                    if (!obj.update)
                    {
                        orders[MarketType.Futures].Clear();
                        orders[MarketType.Currency].Clear();
                    }

                    foreach (var o in obj.user_order)
                    {
                        //Console.WriteLine("[{0}] Got order update {1}", DateTime.Now.ToString("HH:mm:ss.fff"), o.oid);
                        if (orders[o.market].ContainsKey(o.oid))
                        {
                            // Update existing order
                            if (o.status == OrderStatus.Rejected ||
                                o.status == OrderStatus.Canceled ||
                                o.status == OrderStatus.Filled)
                            {
                                Order removed;
                                orders[o.market].TryRemove(o.oid, out removed);
                            }
                            else
                            {
                                orders[o.market][o.oid] = o;
                            }
                        }
                        else
                        {
                            // Add a new one
                            if (o.status == OrderStatus.New ||
                                o.status == OrderStatus.PartiallyFilled)
                            {
                                orders[o.market].TryAdd(o.oid, o);
                            }
                        }
                    }

                    // Signal the ord
                    eventOrderUpdated.Set();

                    // Raise the orders changed event
                    OnOrdersChanged(new EventArgs());
                }

                // Handle instruments dictionary
                if (obj.op == OpType.@private && obj.@private == PacketContent.instruments)
                {
                    var v = obj.instruments;

                    // Clear all instruments dictionaries
                    instruments[MarketType.Futures].Clear();
                    instruments[MarketType.Currency].Clear();

                    // Add all of them to the local cache
                    foreach (var p in v)
                        instruments[p.market_id].TryAdd(p.ticker, p);

                    // Raise the connection event once
                    if (!connected)
                    {
                        connected = true;
                        OnConnect(new EventArgs());
                    }
                }

                // Handle user trades
                if (obj.op == OpType.@private && obj.@private == PacketContent.user_trades)
                {
                    var v = obj.user_trades;

                    // TODO: Add some handling of user trades
                    foreach (var t in v)
                        Console.WriteLine(t);
                }
            }
        }

        protected virtual void OnOrdersChanged(EventArgs e)
        {
            if (OrdersChanged != null)
                OrdersChanged(this, e);
        }

        protected virtual void OnBalanceChanged(EventArgs e)
        {
            if (BalanceChanged != null)
                BalanceChanged(this, e);
        }

        protected virtual void OnConnect(EventArgs e)
        {
            if (ConnectEvent != null)
                ConnectEvent(this, e);
        }
    }
}
