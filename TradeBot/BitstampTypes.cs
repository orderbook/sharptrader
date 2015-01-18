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

namespace MarketMaker.BistampTypes
{
	[Serializable]
	public enum OrderDirection
	{
		bid = 0, // buy
		ask = 1 // sell
	}

    [Serializable]
    public enum WithdrawalType
    {
        sepa = 0,
        bitcoin = 1,
        wire = 2
    }

    [Serializable]
    public enum WithdrawalStatus
    {
        open = 0,
        inprocess = 1,
        finished = 2,
        canceled = 3,
        failed = 4
    }
}