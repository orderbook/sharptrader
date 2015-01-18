using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MarketMaker.Trades
{
    public enum OKCoinFuturesType
    {
        ThisWeek,
        NextWeek,
        Quarter
    }

    [Serializable]
    public class OkcDepth
    {
        public double[][] asks { get; set; }
        public double[][] bids { get; set; }
    }

    [Serializable]
    public class OkcTicker
    {
        public double last { get; set; }
        public double buy { get; set; }
        public double sell { get; set; }
        public double high { get; set; }
        public double low { get; set; }
        public double vol { get; set; }
        public ulong contract_id { get; set; }
        public double unit_amount { get; set; }
    }

    [Serializable]
    public class OkcTickerMsg
    {
        public long date { get; set; }
        public OkcTicker ticker { get; set; }
    }

    [Serializable]
    public class OkcFuturesHolding
    {
        public double buy_amount { get; set; }
        public double buy_available { get; set; }
        public double buy_bond { get; set; }
        public double buy_flatprice { get; set; }
        public double buy_price_avg { get; set; }
        public double buy_profit_lossratio { get; set; }
        public double buy_profit_real { get; set; }
        public ulong contract_id { get; set; }
        public string contract_type { get; set; }
        public long create_date { get; set; }
        public uint lever_rate{ get; set; }
        public double sell_amount { get; set; }
        public double sell_available { get; set; }
        public double sell_bond { get; set; }
        public double sell_flatprice { get; set; }
        public double sell_price_avg { get; set; }
        public double sell_profit_lossratio { get; set; }
        public double sell_profit_real { get; set; }
        public string symbol { get; set; }
    }

    [Serializable]
    public class OkcFuturesHoldingCross
    {
        public double force_liqu_price { get; set; }
        public OkcFuturesHolding[] holding { get; set; }
    }

    [Serializable]
    public class OkcFuturesHoldingFix
    {
        public bool result { get; set; }
        public OkcFuturesHolding[] holding { get; set; }
    }

    [Serializable]
    public class OkcFuturesContracts
    {
        public double available { get; set; }
        public double balance { get; set; }
        public double bond { get; set; }
        public ulong contract_id { get; set; }
        public string contract_type { get; set; }
        public double freeze { get; set; }
        public double profit { get; set; }
        public double unprofit { get; set; }
    }

    [Serializable]
    public class OkcFuturesBalanceInfo
    {
        public double balance { get; set; }
        public OkcFuturesContracts[] contracts { get; set; }
    }

    [Serializable]
    public class OkcFuturesUserInfo
    {
        public OkcFuturesBalanceInfo btc { get; set; }
        public OkcFuturesBalanceInfo ltc { get; set; }
    }

    [Serializable]
    public class OkcFuturesUserInfoFix
    {
        public OkcFuturesUserInfo info { get; set; }
        public bool result { get; set; }
    }
}
