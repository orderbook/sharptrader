/*
 * Program: Trading bot
 * Author: ICBIT team (https://icbit.se)
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

    /* 06:35:00.521 DEBUG: DATA: 4::/icbit:{"channel":"user_order","op":"private","update":false,"private":"user_order",
     * "user_order":[
     * {"oid":"8276056","date":1418508566,"dir":0,"type":0,"price":400,"qty":500,"exec_qty":0,"status":0,"ticker":"BUH5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8276058","date":1418508572,"dir":0,"type":0,"price":450,"qty":500,"exec_qty":0,"status":0,"ticker":"BUH5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8466304","date":1420892854,"dir":1,"type":0,"price":200,"qty":1000,"exec_qty":0,"status":4,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8487123","date":1421143586,"dir":1,"type":0,"price":220,"qty":500,"exec_qty":500,"status":2,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8487135","date":1421143596,"dir":1,"type":0,"price":100,"qty":5000,"exec_qty":0,"status":4,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8504199","date":1421234089,"dir":1,"type":0,"price":192,"qty":1000,"exec_qty":489,"status":1,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8504586","date":1421237093,"dir":1,"type":0,"price":182,"qty":10,"exec_qty":0,"status":4,"ticker":"BUF5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505087","date":1421240319,"dir":1,"type":0,"price":209,"qty":87,"exec_qty":87,"status":2,"ticker":"BUF5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505138","date":1421240620,"dir":1,"type":0,"price":230,"qty":2600,"exec_qty":2600,"status":2,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505199","date":1421241314,"dir":1,"type":0,"price":225,"qty":636,"exec_qty":636,"status":2,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505210","date":1421241409,"dir":1,"type":0,"price":248,"qty":414,"exec_qty":414,"status":2,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505235","date":1421241593,"dir":1,"type":0,"price":272,"qty":130,"exec_qty":130,"status":2,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505253","date":1421241714,"dir":1,"type":0,"price":239,"qty":50,"exec_qty":50,"status":2,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505257","date":1421241785,"dir":1,"type":0,"price":245,"qty":50,"exec_qty":50,"status":2,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505276","date":1421241879,"dir":1,"type":0,"price":277,"qty":5,"exec_qty":5,"status":2,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505283","date":1421241992,"dir":1,"type":0,"price":238,"qty":150,"exec_qty":150,"status":2,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505293","date":1421242141,"dir":1,"type":0,"price":384,"qty":312,"exec_qty":312,"status":2,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505313","date":1421242443,"dir":1,"type":0,"price":247,"qty":1,"exec_qty":1,"status":2,"ticker":"BUU5","token":"0","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505398","date":1421243607,"dir":1,"type":0,"price":182,"qty":10,"exec_qty":0,"status":4,"ticker":"BUF5","token":"111","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505403","date":1421243716,"dir":1,"type":0,"price":182,"qty":10,"exec_qty":0,"status":4,"ticker":"BUF5","token":"111","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505408","date":1421243749,"dir":1,"type":0,"price":182,"qty":10,"exec_qty":0,"status":4,"ticker":"BUF5","token":"111","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505449","date":1421243945,"dir":1,"type":0,"price":182,"qty":10,"exec_qty":0,"status":4,"ticker":"BUF5","token":"111","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505481","date":1421244022,"dir":1,"type":0,"price":182,"qty":10,"exec_qty":0,"status":4,"ticker":"BUF5","token":"111","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8505490","date":1421244056,"dir":1,"type":0,"price":182,"qty":10,"exec_qty":0,"status":4,"ticker":"BUF5","token":"111","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8506391","date":1421249057,"dir":1,"type":0,"price":182,"qty":10,"exec_qty":0,"status":4,"ticker":"BUF5","token":"111","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8506405","date":1421249471,"dir":1,"type":0,"price":182,"qty":10,"exec_qty":0,"status":4,"ticker":"BUF5","token":"111","info":0,"currency":"BTC","market":1,"fills":[]},
     * {"oid":"8506412","date":1421249559,"dir":1,"type":0,"price":182,"qty":10,"exec_qty":0,"status":4,"ticker":"BUF5","token":"111","info":0,"currency":"BTC","market":1,"fills":[]}]}

     */
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
        public long token { get; set; }
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

    /* 06:40:52.182 DEBUG: DATA: 4::/icbit:{"channel":"user_balance","op":"private","private":"user_balance",
     * "user_balance":[{"name":"BTC/USD-9.15","ticker":"BUU5","curid":"1","type":1,"qty":6237,"price":236.95438059601744,"mm":85.87818855,"vm":10.705100996381354,"w":10,"r":1,"inverted":"1"},
     * {"name":"BTC/USD-3.15","ticker":"BUH5","curid":"1","type":1,"qty":4501,"price":328.34346366055394,"mm":66.47963497,"vm":-75.22925583950989,"w":10,"r":1,"inverted":"1"},
     * {"name":"BTC/USD-1.15","ticker":"BUF5","curid":"1","type":1,"qty":2732,"price":336.2673631920281,"mm":41.166923440000005,"vm":-55.355124670250255,"w":10,"r":1,"inverted":"1"},
     * {"name":"Bitcoins","ticker":"BTC","curid":1,"type":0,"qty":"43647140809","upl":-119.8792795133788,"margin":193.52474696000002},
     * {"name":"US Dollars","ticker":"USD","curid":2,"type":0,"qty":"182848945","upl":0,"margin":0}]}
     */

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
