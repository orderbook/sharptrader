/*
 * Program: Trading bot
 * Author: ICBIT Trading Inc. (https://orderbook.net)
 * License: GPLv3 by Free Software Foundation
 * Comments: Definitions of various types used by ICBIT trading API interface
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MarketMaker.IcbitTypes
{
    [Serializable]
    public enum PacketContent
    {
        user_order,
        user_balance,
        user_trades,
        user_info,
        orderbook,
        status,
        ticker,
        instruments
    }

    [Serializable]
    public enum OpType
    {
        @private,
        subscribe,
        unsubscribe,
        chat
    }

    [Serializable]
    public enum OrderStatus
    {
        New = 0,
        PartiallyFilled = 1,
        Filled = 2,
        DoneForToday = 3,
        Canceled = 4,
        Rejected = 5
    }

    [Serializable]
    public struct MarketType
    {
        public const int Currency = 0;
        public const int Futures = 1;
    }

    [Serializable]
    public struct OrderbookEntry
    {
        public long q { get; set; }
        public long p { get; set; }
        public string t { get; set; }
    }

    [Serializable]
    public class Orderbook
    {
        public int s { get; set; }
        //public List<OrderbookEntry> ob { get; set; }
        /* "orderbook":{"s":3,"ob":[{"q":0,"p":0,"t":"b"},
                                    {"q":200000000,"p":270000,"t":"b"},
                                    {"q":1110000,"p":450000,"t":"b"},{"q":20000000,"p":470000,"t":"b"},{"q":191793732,"p":493000,"t":"b"},{"q":40000000,"p":540000,"t":"s"},{"q":200000000,"p":650000,"t":"s"},{"q":100000000,"p":700000,"t":"s"},{"q":5000000,"p":800000,"t":"s"},{"q":100000000,"p":1000000,"t":"s"}]}}*/
    }

    [Serializable]
    public class Fill
    {
        public long ts { get; set; }
        public long price { get; set; }
        public long qty { get; set; }
        public ulong tid { get; set; }
        public long pos { get; set; }
        public long pos_quoted { get; set; }
    }

    [Serializable]
    public class Order
    {
        public long oid { get; set; }
        public long date { get; set; }
        public long dir { get; set; }
        public long type { get; set; }
        public long price { get; set; }
        public long qty { get; set; }
        public long exec_qty { get; set; }
        public OrderStatus status { get; set; }
        public long info { get; set; }
        public ulong token { get; set; }
        public string ticker { get; set; }
        public string currency { get; set; }
        public int market { get; set; }

        public List<Fill> fills { get; set; }

        public Boolean Buy
        {
            get
            {
                if (dir == 1)
                    return true;
                else
                    return false;
            }
        }
    }

    [Serializable]
    public class Position
    {
        public string name { get; set; }
        public string ticker { get; set; }
        public uint curid { get; set; } // currency id, 1 is BTC, 2 is USD
        public int type { get; set; } // "0" for currency, "1" for futures
        public long qty { get; set; }

        // futures specific
        public long price { get; set; }
        public double mm { get; set; }
        public double vm { get; set; }
        public double w { get; set; }
        public double r { get; set; }
        public int inverted { get; set; }
    }

    [Serializable]
    public class Instrument
    {
        public string type { get; set; }
        public int market_id { get; set; }
        public string ticker { get; set; }
        public string name { get; set; }
        public string desc { get; set; }
        public uint curid { get; set; }
        public long im_buy { get; set; }
        public long im_sell { get; set; }
        public long price_min { get; set; }
        public long price_max { get; set; }
        public uint inverted { get; set; }
        public long fee { get; set; }
        public long clr_fee { get; set; }
        public ulong session { get; set; }
        public double r { get; set; }
        public double w { get; set; }

        public double GetPriceMultiplier()
        {
            switch (market_id)
            {
                case MarketType.Currency:
                    return 1.0 / 100000000; // TODO: Implement for different currencies!
                case MarketType.Futures:
                    if (IsFuturesNewStyle())
                        return r;
                    else
                        return 1.0 / 100000000;
                default:
                    return 1;
            }
        }

        private Boolean IsFuturesNewStyle()
        {
            // Legacy contracts with different price multiplicator
            if (market_id != MarketType.Futures) return true;

            switch (ticker)
            {
                case "BUM3":
                case "BUU3":
                case "ESM3":
                case "BUJ3":
                case "BUH3":
                case "BUZ2":
                case "GDG3":
                case "CLG3":
                case "BUK3":
                case "GDJ3":
                case "CLJ3":
                    return false;
                default:
                    return true;
            }
        }
    }

    [Serializable]
    public class Trade
    {
        public int buy { get; set; }
        public int market { get; set; }
        public long price { get; set; }
        public long qty { get; set; }
        public ulong sid { get; set; }
        public string ticker { get; set; }
        public ulong tid { get; set; }
        public long ts { get; set; }
    }

    [Serializable]
    public class UserInfo
    {
        public uint id { get; set; }
        public double fee_discount { get; set; }
        // details: []
    }

    [Serializable]
    public class Packet
    {
        public OpType op { get; set; } //"private","subscribe","unsubscribe","remark"

        public string channel { get; set; }

        public string origin { get; set; } //"broadcast",

        public bool update { get; set; }

        public PacketContent @private { get; set; }

        volatile object _priv; // = null

        public Orderbook orderbook
        {
            get { return _priv as Orderbook; }
            set { _priv = value; }
        }

        public List<Position> user_balance { get; set; }
        public List<Order> user_order { get; set; }
        public List<Instrument> instruments { get; set; }
        public List<Trade> user_trades { get; set; }
        public UserInfo user_info { get; set; }

        public override string ToString()
        {
            switch (op)
            {
                //case OpType.remark:
                    /*if (success.HasValue)
                        return string.Format("Packet={{ remark=[{0}], \tsuccess={1} }}",
                            message != null ? message : "",
                            success.Value == Boolean.True);
                    return string.Format("Packet={{ remark=[{0}] }}",
                        message != null ? message : "");*/
                //case OpType.subscribe:
                //case OpType.unsubscribe:
                    /*return string.Format("Packet={{ {0}subscribe=[{1}] }}",
                        op == OpType.unsubscribe ? "un" : "",
                        channel);*/
                //case OpType.@private:
                    /*return string.Format("Packet={{ {0} }}", _priv.ToString());*/
                default:
                    return "Packet!";
            }
            return "<unknown>";
        }
    }

    // 01:11:06.534 DEBUG: DATA: 4::/icbit:{"channel":"status","op":"private","private":"status","status":"Offline"}
}
