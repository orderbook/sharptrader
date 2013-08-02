using System;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

namespace WebSockets.Utils
{
	public static class Helper
	{
		#region init
		//=========================================================================================

		// object for locks
		static readonly string _sync = Guid.NewGuid().ToString();

		static Helper()
		{
			// handlers already not null
			UnexpectedException += ( err ) => Log( "UNEXPECTED", err );

#if OS_WINDOWS
			long f = 0;
			QueryPerformanceFrequency( out f );
			s_invFreq = 1.0 / f;
#endif

			initDecimalFracs();

			_rnd = new Random( ( int )(Helper.UptimeSeconds * 1000000) );
		}
		#endregion init

		#region "String".Format extension
		//=========================================================================================

		public static string Format( this string format, params object[] args )
		{
			return string.Format( format, args );
		}
		#endregion format

		#region log
		//=========================================================================================

		static readonly StringBuilder _sb = new StringBuilder( 1024 );
		private static string getString( string category, string format, params object[] args )
		{
			lock (_sync)
			{
				_sb.Clear();
				_sb.AppendFormat( "{0:hh:mm:ss.fff} {1}: ", DateTime.Now, category );
				_sb.AppendFormat( format, args );
				return _sb.ToString();
			}
		}

		public static void Log( object obj )
		{
			Trace.WriteLine( getString( "LOG", obj.ToString() ) );
		}
		public static void Log( string format, params object[] args )
		{
			Trace.WriteLine( getString( "LOG", format, args ) );
		}
		public static void LogCat( string category, string format, params object[] args )
		{
			Trace.WriteLine( getString( category, format, args ) );
		}

		[ConditionalAttribute( "DEBUG" )]
		public static void Debug( object obj )
		{
			System.Diagnostics.Debug.WriteLine( getString( "DEBUG", obj.ToString() ) );
		}
		[ConditionalAttribute( "DEBUG" )]
		public static void Debug( string format, params object[] args )
		{
			System.Diagnostics.Debug.WriteLine( getString( "DEBUG", format, args ) );
		}
		[ConditionalAttribute( "DEBUG" )]
		public static void DebugCat( string category, string format, params object[] args )
		{
			System.Diagnostics.Debug.WriteLine( getString( category, format, args ) );
		}

		#endregion

		#region string => types
		//=========================================================================================

		// dont create more strings(new objects)
		// just parse string to nondigit char (and non DOT)

		// alg is 3times faster than Int64.Parse at Core2 Duo 3GHz
		public static long Int64FromString( string str, int pos, int count )
		{
			count = Math.Min( count, str.Length - pos );
			bool neg = str[ pos ] == '-';
			if (neg)
				++pos;

			long n = 0;
			for (count += pos; pos < count; ++pos)
			{
				char ch = str[ pos ];
				if ('0' <= ch && ch <= '9')
				{
					n += n;
					n += (n << 2) + (ch - '0');
				}
				else
					break;
			}

			return neg ? -n : n;
		}

		// alg is 6times faster than Double.Parse at Core2 Duo 3GHz
		public static double DoubleFromString( string str, int pos, int count, char dot = '.' )
		{
			count = Math.Min( count, str.Length - pos );
			bool neg = str[ pos ] == '-';
			if (neg)
				++pos;

			double n = 0;
			for (count += pos; pos < count; ++pos)
			{
				char ch = str[ pos ];
				if ('0' <= ch && ch <= '9')
					n = n * 10 + (ch - '0');
				else
					break;
			}

			if (pos < count && str[ pos ] == dot)
			{
				double f = 0.1;
				for (++pos; pos < count; ++pos, f *= 0.1)
				{
					char ch = str[ pos ];
					if ('0' <= ch && ch <= '9')
						n += (ch - '0') * f;
					else
						break;
				}
			}

			return neg ? -n : n;
		}

		static readonly decimal[] s_fracDec = new decimal[ 20 ];
		static void initDecimalFracs()
		{
			decimal t = 1m;
			for (int i = 0; i < s_fracDec.Length; ++i)
				s_fracDec[ i ] = t *= 0.1m;
		}

		// alg is 2times faster than Decimal.Parse at Core2 Duo 3GHz
		public static decimal DecimalFromString( string str, int pos, int count, char dot = '.' )
		{
			count = Math.Min( count, str.Length - pos );
			bool neg = str[ pos ] == '-';
			if (neg)
				++pos;

			long n = 0;
			for (count += pos; pos < count; ++pos)
			{
				char ch = str[ pos ];
				if ('0' <= ch && ch <= '9')
				{
					n += n;
					n += (n << 2) + (ch - '0');
				}
				else
					break;
			}
			decimal res = n;

			if (pos < count && str[ pos ] == dot)
			{
				n = 0;
				int cntf = 0;
				for (++pos; pos < count; ++pos, ++cntf)
				{
					char ch = str[ pos ];
					if ('0' <= ch && ch <= '9')
					{
						n += n;
						n += (n << 2) + (ch - '0');
					}
					else
						break;
				}
				res += n * s_fracDec[ cntf ];
			}

			return neg ? -res : res;
		}

		#endregion

		#region uptime
		//=========================================================================================

#if OS_WINDOWS
		public static double UptimeSeconds
		{
			get
			{
				long c = 0;
				QueryPerformanceCounter( out c );
				return c * s_invFreq;
			}
		}

		static readonly double s_invFreq;

		[DllImport( "kernel32" )]
		static extern int QueryPerformanceFrequency( out long value );
		[DllImport( "kernel32" )]
		static extern int QueryPerformanceCounter( out long value );

#elif OS_ANDROID
#endif
		#endregion uptime

		#region random
		//=========================================================================================

		// Random for all
		static readonly Random _rnd;

		public static Random Random { get { return _rnd; } }
		#endregion random

		#region unexpected errors catcher
		//=========================================================================================

		static volatile Action<Exception> _onUnexpected;
		public static event Action<Exception> UnexpectedException
		{
			add { _onUnexpected += value; }
			remove { _onUnexpected -= value; }
		}

		public static void CatchUnexpected( Action todo )
		{
			try { todo(); }
			catch (Exception err)
			{
				 // handler not null, it contains our Log(...) (see static constructor)
				RaiseUnexpected( err );
			}
		}

		// то же что и выше, но возвращает результат
		public static T CatchUnexpected<T>( Func<T> todo, T def = default( T ) )
		{
			try { return todo(); }
			catch (Exception err)
			{
				// handler not null, it contains our Log(...) (see static constructor)
				RaiseUnexpected( err );
			}
			return def;
		}

		public static void RaiseUnexpected( Exception err )
		{
			try { _onUnexpected( err ); }
			catch { /*do nothing*/ }
		}
		#endregion unexpected errors catcher

		#region checkers
		//=========================================================================================

		public static void Check<E>( bool cond, params object[] args ) where E : Exception
		{
			if (!cond)
			{
				var err = Activator.CreateInstance( typeof( E ), args );
				throw err as Exception;
			}
		}

		public static void Check<E>( bool cond, string message, params object[] args ) where E : Exception
		{
			if (!cond)
			{
				var msg = CatchUnexpected( () => message.Format( args ), message );
				var err = Activator.CreateInstance( typeof( E ), msg );
				throw err as Exception;
			}
		}

		public static void CheckArg( bool cond, string message = "arg" )
		{
			if (!cond)
				throw new ArgumentException( message );
		}

		public static void CheckArg( bool cond, string message, params object[] args )
		{
			if (!cond)
			{
				var msg = CatchUnexpected( () => message.Format( args ), message );
				throw new ArgumentException( msg );
			}
		}

		public static void CheckArgNotNull<T>( T t, string message = null )
			where T : class
		{
			if (t == null)
			{
				if (message == null)
					message = typeof( T ).Name;
				throw new ArgumentNullException( message );
			}
		}

		public static void CheckArgNotNull<T>( T t, string message, params object[] args )
			where T : class
		{
			if (t == null)
			{
				var msg = string.Empty;
				if (message == null)
					msg = typeof( T ).Name;
				else
					msg = CatchUnexpected( () => message.Format( args ), message );
				throw new ArgumentNullException( msg );
			}
		}

		public static void CheckOperation( bool cond, string message = "denied" )
		{
			if (!cond)
				throw new InvalidOperationException( message );
		}

		public static void CheckOperation( bool cond, string message, params object[] args )
		{
			if (!cond)
			{
				message = CatchUnexpected( () => message.Format( args ), message );
				throw new InvalidOperationException( message );
			}
		}

		#endregion checkers
	}
}

