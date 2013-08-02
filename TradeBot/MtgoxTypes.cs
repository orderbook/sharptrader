/*
 * Program: Arbitrage trading bot
 * Author: ICBIT team (https://icbit.se), SocketIO_gox authors
 * License:
 * Comments: Code in this file mostly consists of code from the SocketIO_gox package.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//using fastJSON;

namespace MarketMaker.MtGoxTypes
{
	[Serializable]
	public enum OrderDirection
	{
		ask = 1, // sell
		bid = 2 // buy
	}

    [Serializable]
    public enum OrderStatus
    {
        Active,
        NotSufficientFunds,
        Pending,
        Invalid
    }

	[Serializable]
	public enum Boolean
	{
		N = 0,
		//No = 0,
		False = 0,

		Y = 1,
		//Yes = 1,
		True = 1,
	}

	[Serializable]
	public enum Currency
	{
		USD,
		GBP,
		EUR,
		AUD,
		PLN,
		CAD,
		CNY,
		SEK,
		JPY,
		CHF,
		DKK,
		HKD,
		NZD,
		RUB,
		THB,
		SGD,

		BTC
	}

	[Serializable]
	public enum PacketContent
	{
		depth,
		trade,
		ticker,
		oldticker
	}

	[Serializable]
	public enum OpType
	{
		@private,
		subscribe,
		unsubscribe,
		remark
	}

	//================================================================================================
	[Serializable]
	public class Depth
	{
		public int type { get; set; } //2,
		public OrderDirection type_str { get; set; } //"bid",
		public decimal volume { get; set; } //"-0.2947",
		public long volume_int { get; set; } //"-29470000",
		public decimal price { get; set; } //"3.91312",
		public long price_int { get; set; } //"391312",
		public Currency item { get; set; } //"BTC",
		public Currency currency { get; set; } //"USD",
		public long now { get; set; } //"1325074957181108",
		public int total_volume_int { get; set; } //"0"

		[NonSerialized]
		static readonly string N3 = "N3";
		[NonSerialized]
		static readonly string N5 = "N5";
		[NonSerialized]
		static readonly string N8 = "N8";
		[NonSerialized]
		static readonly DateTime Era1970 = new DateTime( 1970, 1, 1 );
		public override string ToString()
		{
			return string.Format( "Depth={{ price={0} {1}, \tvol={2}, \tdir={3}, \twhen={4:yyyyMMdd_hhmmss.ffffff} }}",
				price.ToString( currency != Currency.JPY ? N5 : N3 ), currency,
				volume.ToString( N8 ),
				type_str, UtcWhen() );
		}

		// not props for serialization(to JSON) issue
		public DateTime UtcWhen()
		{
			// *10: mks => ticks
			var ts = now << 1;
			ts += ts << 2;
			return Era1970 + TimeSpan.FromTicks( ts );
		}
		public DateTime When() { return UtcWhen().ToLocalTime(); }
	}

	//================================================================================================
	[Serializable]
	public class OldTicker
	{
		public decimal high { get; set; } //"4.18888",
		public decimal low { get; set; } //"4.18888",
		public decimal avg { get; set; } //"4.18888",
		public decimal vwap { get; set; } //"4.18888",
		public decimal vol { get; set; } //"4.18888",
		public decimal last_all { get; set; } //"4.18888",
		public decimal last_local { get; set; } //"4.18888",
		public decimal last { get; set; } //"4.18888",
		public decimal buy { get; set; } //"4.18888",
		public decimal sell { get; set; } //"4.18888",

		[NonSerialized]
		static readonly string N3 = "N3";
		[NonSerialized]
		static readonly string N5 = "N5";
		[NonSerialized]
		static readonly string N8 = "N8";
		public override string ToString()
		{
			return String.Format( "OldTicker={{ bid={0}, \task={1}, \tlo={2}, \thi={3}, \tlast={4}, \tvol={5} }}",
				buy.ToString( N5 ),
				sell.ToString( N5 ),
				low.ToString( N5 ),
				high.ToString( N5 ),
				last.ToString( N5 ),
				vol.ToString( N8 ) );
		}
	}

	//================================================================================================
	[Serializable]
	public class TickerValue
	{
		public decimal value { get; set; } //"4.18888",
		public long value_int { get; set; } //"418888",
		public string display { get; set; } //"$4.18888",
		public Currency currency { get; set; } //"USD"
	}

	[Serializable]
	public class Ticker
	{
		public TickerValue high { get; set; }
		public TickerValue low { get; set; }
		public TickerValue avg { get; set; }
		public TickerValue vwap { get; set; }
		public TickerValue vol { get; set; }
		public TickerValue last_local { get; set; }
		public TickerValue last { get; set; }
		public TickerValue last_orig { get; set; }
		public TickerValue last_all { get; set; }
		public TickerValue buy { get; set; }
		public TickerValue sell { get; set; }

		public override string ToString()
		{
			return String.Format( "Ticker={{ bid={0}, \task={1}, \tlo={2}, \thi={3}, \tlast={4}, \tvol={5} }}",
				buy != null ? buy.display : "*",
				sell != null ? sell.display : "*",
				low != null ? low.display : "*",
				high != null ? high.display : "*",
				last != null ? last.display : "*",
				vol != null ? vol.display : "*" );
		}
	}

    //================================================================================================
    [Serializable]
    public class Order
    {
        public string oid { get; set; }
        public Currency currency { get; set; }
        public Currency item { get; set; }
        public OrderDirection type { get; set; }
        public double amount { get; set; }
        public long amount_int { get; set; }
        public double price { get; set; }
        public long price_int { get; set; }
        public OrderStatus status { get; set; }
        public string real_status { get; set; }
        public int dark { get; set; }
        public int date { get; set; }
        public long priority { get; set; }
    }

	//================================================================================================
	[Serializable]
	public class Trade
	{
		public long tid { get; set; } //"1325089607206362",
		public string type { get; set; } //"trade",
		public int date { get; set; } //1325089607,
		public decimal amount { get; set; } //0.01973552,
		public long amount_int { get; set; } //"1973552",
		public decimal price { get; set; } //4.13038,
		public long price_int { get; set; } //"413038",
		public Currency item { get; set; } //"BTC",
		public Currency price_currency { get; set; } //"USD",
		public OrderDirection trade_type { get; set; } //"ask",
		public Boolean primary { get; set; } //"Y",
		public string properties { get; set; } //"limit"

		[NonSerialized]
		static readonly string N3 = "N3";
		[NonSerialized]
		static readonly string N5 = "N5";
		[NonSerialized]
		static readonly string N8 = "N8";
		[NonSerialized]
		static readonly DateTime Era1970 = new DateTime( 1970, 1, 1 );
		public override string ToString()
		{
			return string.Format( "Trade={{ price={0} {1}, \tvol={2}, \tdir={3}, \twhen={4:yyyyMMdd_hhmmss.ffffff} }}",
				price.ToString( price_currency != Currency.JPY ? N5 : N3 ), price_currency,
				amount.ToString( N8 ),
				trade_type, UtcWhen() );
		}

		// not props for serialization(to JSON) issue
		public DateTime UtcWhen()
		{
			// *10: mks => ticks
			var ts = tid << 1;
			ts += ts << 2;
			return Era1970 + TimeSpan.FromTicks( ts );
		}
		public DateTime When() { return UtcWhen().ToLocalTime(); }

        /// <summary>
        /// Parses the JSON data returned by the 0/data/getTrades.php method
        /// </summary>        
        /*
        public static List<Trade> getObjects(string jsonDataStr)
        {
            List<Trade> tradeList = new List<Trade>();
            string json = jsonDataStr;
            var serializer = new JavaScriptSerializer();
            serializer.RegisterConverters(new[] { new DynamicJsonConverter() });
            dynamic obj = serializer.Deserialize(json, typeof(object));
            for (int i = 0; i < obj.Length; i++)
            {
                Trade trade = new Trade();
                trade.date = obj[i].date;
                trade.price = Double.Parse(obj[i].price);
                trade.amount = Double.Parse(obj[i].amount);
                trade.price_int = Int64.Parse(obj[i].price_int);
                trade.amount_int = Int64.Parse(obj[i].amount_int);
                trade.tid = obj[i].tid;
                if (Enum.IsDefined(typeof(MtGoxCurrencySymbol), obj[i].price_currency))
                    trade.price_currency = (MtGoxCurrencySymbol)Enum.Parse(typeof(MtGoxCurrencySymbol), obj[i].price_currency, true);
                trade.item = obj[i].item;
                if (Enum.IsDefined(typeof(OrderDirection), obj[i].trade_type))
                    trade.trade_type = (OrderDirection)Enum.Parse(typeof(OrderDirection), obj[i].trade_type, true);
                trade.primary = obj[i].primary;
                tradeList.Add(trade);
                if (i > 100)
                    break;
            }
            return tradeList;
        }*/
	}

	//================================================================================================
	[Serializable]
	public class Packet
	{
		public OpType op { get; set; } //"private","subscribe","unsubscribe","remark"

		public string channel { get; set; } //"24e67e0d-1cad-4cc0-9e7a-f8523ef460fe","depth"

		public string message { get; set; } //"Now online (no channels)",
		public Boolean? success { get; set; } //

		public string origin { get; set; } //"broadcast",

		public PacketContent @private { get; set; } //"depth","trade","ticker","oldticker"

		volatile object _priv; // = null
		public Depth depth
		{
			get { return _priv as Depth; }
			set { _priv = value; }
		}
		public Trade trade
		{
			get { return _priv as Trade; }
			set { _priv = value; }
		}
		public Ticker ticker
		{
			get { return _priv as Ticker; }
			set { _priv = value; }
		}
		public OldTicker oldticker
		{
			get { return _priv as OldTicker; }
			set { _priv = value; }
		}

		public override string ToString()
		{
			switch (op)
			{
				case OpType.remark:
					if (success.HasValue)
						return string.Format( "Packet={{ remark=[{0}], \tsuccess={1} }}",
							message != null ? message : "",
							success.Value == Boolean.True );
					return string.Format( "Packet={{ remark=[{0}] }}",
						message != null ? message : "" );
				case OpType.subscribe:
				case OpType.unsubscribe:
					return string.Format( "Packet={{ {0}subscribe=[{1}] }}",
						op == OpType.unsubscribe ? "un" : "",
						channel );
				case OpType.@private:
					return string.Format( "Packet={{ {0} }}", _priv.ToString() );
			}
			return "<unknown>";
		}
	}

    [Serializable]
    public class TickerContainer
    {
        public string result { get; set; }
        public Ticker ret { get; set; }
    }

    [Serializable]
    public class TickerContainerAPIv0
    {
        public Ticker ticker { get; set; }
    }

    [Serializable]
    public class HistoryItem
    {
        public string Login { get; set; }
        public int Index { get; set; }
        public List<string> Rights { get; set; }
        public string Language { get; set; }
        public DateTime Created { get; set; }
        public DateTime Last_Login { get; set; }
        //public List<MtGoxWallet> Wallets { get; set; }
        public double Trade_Fee { get; set; }
        public HistoryItem()
        { }
    }

	//================================================================================================
    [Serializable]
    public class OrderResponse
    {
        public string status { get; set; }
        public string oid { get; set; }
        public List<Order> orders { get; set; }
    }

    //================================================================================================
    [Serializable]
    public class OrderResponseWithBalance
    {
        //public long usds { get; set; }
        //public long btcs { get; set; }
        public List<Order> orders { get; set; }
    }

}

/*
 * old descr:
 *
op:remark
  message:<message text>
  success:<success boolean>
op:subscribe
  channel:<channel uuid>
op:unsubscribe
  channel:<channel uuid>
op:private
  channel:<channel uuid>
  origin:broadcast
  private:depth
    depth:{
      volume:<volume>
      price:<price>
      type:<order type>
    }
  private:ticker
    ticker:{
      high:<high price>
      low:<low price>
      vol:<volume>
      buy:<buy price>
      sell:<sell price>
    }
  private:trade
    trade:{
      date:<int time>
      amount:<amount float>
      type:trade
      price:<trade price>
    }
  private:oldticker
    oldticker:{
	  high:<price>
      low:<price>
	  avg:<price>
	  vwap:<amount> ?6.32829176
	  vol:128972
      last_all:6.40636
      last_local:6.40636
      last:6.40636
      buy:6.40636
      sell:6.44798
    }
  private:order_add
    order_add:{
      oid:<order id int>
      price:<limit price>
      date:<int time>
      amount:<amount>
      status:<status int>
      darkStatus:<0 or 1>
    }
  private:order_rem
    order_rem:{
      oid:<order id int>
    }
*/

/*
{"private":"oldticker","oldticker":{"high":6.89,"low":6.001,"avg":6.346081923,"vwap":6.32829176,"vol":128972,"last_all":6.40636,"last_local":6.40636,"last":6.40636,"buy":6.40636,"sell":6.44798}}
A first chance exception of type 'System.ArgumentException' occurred in mscorlib.dll*/