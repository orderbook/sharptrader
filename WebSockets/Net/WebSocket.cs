using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Net.Security;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

// something as ref parameter not treated as volatile
#pragma warning disable 420

using WebSockets.Utils;

namespace WebSockets.Net
{
	//=============================================================================================

	public enum ShutdownCode : short
	{
		Normal = 1000,
		GoingAway = 1001,
		ProtocolError = 1002,
		UnsupportedData = 1003,
		InvalidData = 1007,
		PolicyViolation = 1008,
		MessageTooBig = 1009,
		ServerError = 1011,
		TlsHandshake = 1015
	}

	//=============================================================================================

	// imho, delegates less suitable
	public interface IWebSocketHandler
	{
		// all methods can be invoked from working threads

		void OnString( WebSocket sender, string data );
		void OnBinary( WebSocket sender, byte[] data );

		void OnPong( WebSocket sender, byte[] data );

		void OnError( WebSocket sender, string op, Exception err );

		void OnShutdown( WebSocket sender, ShutdownCode code, byte[] data );

		void OnClosed( WebSocket sender );
	}

	//=============================================================================================

	// no supports framing. all frames must be FIN.
	// no supports long messages: >= 64K
	public class WebSocket : IDisposable
	{
		// not need attribute [beforeFieldInit]
		static WebSocket() { }

		readonly Encoding _enc;

		public WebSocket() : this( Encoding.UTF8 ) { }

		// if I known that all messages will be ASCII, I can optimize strings exchange
		public WebSocket( Encoding encoding )
		{
			Helper.CheckArgNotNull( encoding, "encoding" );
			_enc = encoding;

			RequestHeaders = new WebHeaderCollection();
			ResponseHeaders = new WebHeaderCollection();
			HttpHeader = "GET {0} HTTP/1.1";

			prepareHeaders();
		}

		// props

		// version of websocket-protocol
		volatile string _version = "13";
		public string Version
		{
			get { return _version; }
			set
			{
				Helper.CheckArg( string.IsNullOrEmpty( value ), "Version" );
				_version = value;
			}
		}

		// connection stream
		volatile Stream _stream;
		public Stream Stream
		{
			get { return _stream; }
			private set
			{
				var stream = Interlocked.Exchange( ref _stream, value );
				if (stream != null)
					stream.Close();
			}
		}

		// callbacks
		volatile IWebSocketHandler _handler;
		public IWebSocketHandler Handler
		{
			get { return _handler; }
			set { _handler = value; }
		}

		// masking client frames
		// MtGox not supports masking (at 2011.12.27), so default is false
		volatile bool _masking = false;
		public bool IsMasking
		{
			get { return _masking; }
			set
			{
				Helper.CheckOperation( _stream == null, "Cannot change IsMasking when websocket is connected" );
				_masking = value;
			}
		}

		// key for HTTP-headers in Re*Headers
		// usualy contains "GET {0} HTTP/1.1" for RequestHeaders
		// and "HTTP/1.1 101 Switching Protocols" for ResponseHeaders
		static readonly string _http = "HTTP";
		public static string HttpKey { get { return _http; } }

		// GET {0} HTTP/1.1
		public static string HttpHeader { get; set; }

		// for extended setup by client
		public WebHeaderCollection RequestHeaders { get; private set; }

		// not good if server responses with 2 equals(key) lines:
		// Sec-WebSocket-Version: 13
		// Sec-WebSocket-Version: 8, 7
		public WebHeaderCollection ResponseHeaders { get; private set; }

		public void Open( string url )
		{
			Close();
			try
			{
				makeHandshake( url );
				_isShuted = false;
			}
			catch (Exception err)
			{
				Helper.Log( "ERROR: " + err );
				throw;
			}
		}

		public Stream Detach()
		{
			_isShuted = true;
			return Interlocked.Exchange( ref _stream, null );
		}

		public void Close()
		{
			_isShuted = true;
			//RequestHeaders.Clear();
			ResponseHeaders.Clear();
			Stream = null;
		}

		public void Ping( byte[] data = null )
		{
			Helper.CheckArg( data == null || data.Length < 126, "Ping.data.Length must be less than 126" );
			send( 0x9, data, true );
		}

		volatile bool _isShuted = true;
		public void Shutdown( ShutdownCode? code = null, byte[] data = null )
		{
			Helper.CheckArg( data == null || data.Length < 124, "Shutdown.data.Length must be less than 124" );
			Helper.CheckArg( code.HasValue || data == null, "If data is not null than ShutdownCode must be set" );

			if (code.HasValue)
			{
				var bcode = BitConverter.GetBytes( IPAddress.HostToNetworkOrder( ( short )code.Value ) );
				if (data != null)
				{
					var buf = new byte[ 2 + data.Length ];
					Array.Copy( bcode, buf, 2 );
					Array.Copy( data, 0, buf, 2, data.Length );
					data = buf;
				}
				else
					data = bcode;
			}
			send( 0x8, data, true );
		}

		// known exactly length of byte buffer
		public void SendAscii( string str, bool flush = true )
		{
			send( str, Encoding.ASCII, str.Length, flush );
		}

		public void Send( string str, bool flush = true )
		{
			var len = _enc.GetByteCount( str );
			send( str, _enc, len, flush );
		}

		public void Send( byte[] data, bool flush = true )
		{
			Helper.CheckArgNotNull( data, "data" );
			send( 2, data, flush );
		}

		#region implementation

		#region handshake
		static readonly string sSecSalt = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

		void makeHandshake( string url )
		{
			Uri uri = new Uri( url );
			string host = uri.DnsSafeHost;
			string path = uri.PathAndQuery;
			bool isWss = url.ToLower().StartsWith( "wss" );
			string origin = uri.Scheme + "://" + host;

			Socket sock = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );
			sock.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.NoDelay, true );
			IPAddress addr = Dns.GetHostAddresses( host )[ 0 ];
			int port = uri.Port;
			port = port < 0 ? (isWss ? 443 : 80) : port;
			sock.Connect( new IPEndPoint( addr, port ) );

			Stream stream = new NetworkStream( sock, true );
			if (isWss)
			{
				var ssl = new SslStream( stream, false, validateServerCertificate );
				ssl.AuthenticateAsClient( host );
				stream = ssl;
			}

			// create HttpKey header
			var bsec = new byte[ 16 ];
			using (var rnd = RandomNumberGenerator.Create())
				rnd.GetBytes( bsec );
			var ssec = Convert.ToBase64String( bsec );
			var whs = RequestHeaders;
			whs[ HttpRequestHeader.Host ] = host;
			whs[ sOrigin ] = origin;
			whs[ sSecOrigin ] = origin;
			whs[ sSecKey ] = ssec;
			string sreq0 = string.Format( HttpHeader, path );
			string sreq = sreq0 + "\r\n" + whs.ToString();
			whs[ HttpKey ] = sreq0;

			// send HttpKey-header to server (UTF8. ignore _enc)
			int len = Encoding.UTF8.GetBytes( sreq, 0, sreq.Length, _buf, 0 );
			stream.Write( _buf, 0, len );

			// read response from server
			whs = readHttpResponse( stream, ResponseHeaders );
			// Sincerely yours, Captain Obviousness

			// check headers
			if (!"HTTP/1.1 101 Switching Protocols".Equals( whs[ HttpKey ] )
				|| !sWebsocket.Equals( whs[ HttpResponseHeader.Upgrade ] )
				|| !sUpgrade.Equals( whs[ HttpResponseHeader.Connection ] ))
			{
				throw new FormatException( "Invalid handshake response" );
			}

			// check SecKey
			ssec += sSecSalt;
			using (var sha1 = new SHA1Managed())
			{
				sha1.Initialize();
				bsec = sha1.ComputeHash( Encoding.ASCII.GetBytes( ssec ) );
			}
			var shash = Convert.ToBase64String( bsec );
			if (whs[ sSecAccept ] != shash)
				throw new FormatException( "Sec-WebSocket-Accept not equals to SHA1-hash" );

			Stream = stream;
			beginRecv( stream, 0 );
		}
		#endregion handshake

		#region recv & send
		const int EOL = (( int )'\r' << 8) + ( int )'\n';
		const int EOF = (EOL << 16) + EOL;
		/// <summary>
		/// Reads stream until "\r\n\r\n"
		/// Every line puts into WebHeaderCollection
		/// </summary>
		/// <param name="sin">Input stream</param>
		/// <returns>Response from server</returns>
		WebHeaderCollection readHttpResponse( Stream sin, WebHeaderCollection whs )
		{
			int eof = 0, pos = 0;
			while (true)
			{
				var ib = sin.ReadByte();
				if (ib < 0)
					throw new IOException( "stream was closed" );

				byte b = ( byte )ib;
				_buf[ pos++ ] = b;

				eof = (eof << 8) + b;
				if (eof == EOF)
					return whs;

				if ((eof & 0xffff) == EOL)
				{
					// UTF8. ignore _enc
					var line = Encoding.UTF8.GetString( _buf, 0, pos - 2 );
					pos = line.IndexOf( ':' );
					if (pos < 0)
						whs[ HttpKey ] = line;
					else
					{
						var key = line.Substring( 0, pos );
						var val = line.Substring( pos + 1 ).TrimStart();
						whs[ key ] = val;
					}
					pos = 0;
				}
			}
		}

		readonly byte[] _buf = new byte[ 65 * 1024 ];
		bool beginRecv( Stream stream, int pos )
		{
			try
			{
				var state = new Tuple<Stream, int>( stream, pos );
				stream.BeginRead( _buf, pos, _buf.Length - pos, recv, state );
			}
			catch (Exception err)
			{
				onError( "recv", err );
				return false;
			}
			return true;
		}

		const int c64K = 64 * 1024;
		readonly byte[] _sbuf = new byte[ c64K + 16 ];
		private void send( string str, Encoding enc, int len, bool flush )
		{
			var buf = _sbuf;
			lock (buf)
			{
				var ofs = prepareFrame( 1, len );
				enc.GetBytes( str, 0, str.Length, buf, ofs );
				maskFrame( len, ofs );
				send( buf, len + ofs, flush );
			}
		}

		private void send( int payload, byte[] data, bool flush )
		{
			Helper.CheckOperation( !_isShuted, "Stream is shutdowned" );

			var len = data != null ? data.Length : 0;
			var buf = _sbuf;
			lock (buf)
			{
				var ofs = prepareFrame( payload, len );
				if (len > 0)
				{
					Array.Copy( data, 0, buf, ofs, len );
					maskFrame( len, ofs );
				}
				_isShuted = _isShuted || (payload == 8);
				send( buf, len + ofs, flush );
			}

		}

		private void send( byte[] buf, int len, bool flush )
		{
			Stream stream = _stream;
			try
			{
				if (stream != null)
				{
					// we cant send async to SslStream
					// it raises exception if send async more than one
					stream.Write( buf, 0, len );
					if (flush)
						stream.Flush();
				}
			}
			catch (Exception err) 
			{ 
				// if not closed
				if (_stream == stream)
					onError( "send", err );
			}
		}
		#endregion recv & send

		#region process sending frame
		// must be invoked in lock(_sbuf)
		// returns length of frame header
		int prepareFrame( int payload, int len )
		{
			//var len = data != null ? data.Length : 0;
			Helper.CheckArg( len < c64K, "length must be less 64K. NB: string.Length <= UTF8(string).Length for non-ASCII" );
			Helper.CheckArg( ( uint )payload < 16, "unknow data's payload type" );

			var less126 = len < 126;
			var ofs = less126 ? 2 : 4;

			var buf = _sbuf;
			buf[ 0 ] = ( byte )payload;
			buf[ 0 ] |= 0x80; // FIN

			// length
			if (less126)
				buf[ 1 ] = ( byte )len;
			else
			{
				buf[ 1 ] = 126;
				var ulen = IPAddress.HostToNetworkOrder( len );
				buf[ 2 ] = ( byte )(ulen & 0xff);
				buf[ 3 ] = ( byte )(ulen >> 8);
			}

			// masking
			if (IsMasking)
			{
				buf[ 1 ] |= 0x80; // MASKing
				var mask = Helper.Random.Next( 0x40000000, Int32.MaxValue );
				mask = IPAddress.HostToNetworkOrder( mask );

				buf[ ofs++ ] = ( byte )(mask & 0xff);
				mask >>= 8;
				buf[ ofs++ ] = ( byte )(mask & 0xff);
				mask >>= 8;
				buf[ ofs++ ] = ( byte )(mask & 0xff);
				mask >>= 8;
				buf[ ofs++ ] = ( byte )(mask & 0xff);
			}

			return ofs;
		}

		void maskFrame( int len, int ofs )
		{
			if (!IsMasking)
				return;

			var buf = _buf;
			for (int i = 0; i < len; ++i)
				buf[ i + ofs ] ^= buf[ ofs - 4 + (i & 0x3) ];
		}
		#endregion process sending frame

		#region misc
		static readonly string sOrigin = "Origin";
		static readonly string sUpgrade = "Upgrade";
		static readonly string sWebsocket = "websocket";
		static readonly string sSecOrigin = "Sec-WebSocket-Origin";
		static readonly string sSecKey = "Sec-WebSocket-Key";
		static readonly string sSecVersion = "Sec-WebSocket-Version";
		static readonly string sSecAccept = "Sec-WebSocket-Accept";

		private void prepareHeaders()
		{
			var whs = RequestHeaders;
			whs[ HttpRequestHeader.Host ] = "";
			whs[ HttpRequestHeader.Upgrade ] = sWebsocket;
			whs[ HttpRequestHeader.Connection ] = sUpgrade;
			whs[ sOrigin ] = "";
			whs[ sSecOrigin ] = "";
			whs[ sSecKey ] = "";
			whs[ sSecVersion ] = Version;
		}

		static bool validateServerCertificate( object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors )
		{
			return true;
		}
		#endregion misc

		#endregion implementation

		#region IDisposable implementation
		volatile int _disposed = 0;
		~WebSocket()
		{
			Dispose( false );
		}
		public void Dispose()
		{
			if (_disposed == 0)
				Dispose( true );
		}
		void Dispose( bool disposing )
		{
			Handler = null;
			if (disposing && Interlocked.CompareExchange( ref _disposed, 1, 0 ) == 0)
			{
				GC.SuppressFinalize( this );
				Stream = null;
			}
		}
		#endregion

		#region async callbacks

		void recv( IAsyncResult ar )
		{
			var state = ar.AsyncState as Tuple<Stream, int>;
			try
			{
				int len = state.Item1.EndRead( ar );

				if (len == 0)
				{
					onClosed();
					return;
				}

				int was = state.Item2;
				int all = was + len;

				while (all > 1)
				{
					byte b = _buf[ 0 ];
					if (b == 0)
					{
						// old protocol ???
						int pos = Array.IndexOf<byte>( _buf, 255, was, len );
						if (pos < 0)
							break;
						onString( _enc.GetString( _buf, 1, was + pos - 1 ) );
						Array.Copy( _buf, pos + 1, _buf, 0, all -= pos + 1 );
					}
					else
					{
						// last frame in message (no glue yet)
						Helper.CheckArg( (b & 0x80) != 0, "This WebSocket\'s implementation doesnt support non-FIN frames" );
						b ^= 0x80;
						b &= 0x0f; // get payload

						int n = _buf[ 1 ], tn = 2;
						// no masking from server
						Helper.CheckArg( (n & 0x80) == 0, "Unexpected WebSocket.frame-masking from server" );
						if (n >= 126)
						{
							// >64K is too big for us now
							Helper.CheckArg( n < 127, "This WebSocket\'s implementation doesnt support frames with 8bytes-length" );
							if (all < 4)
								break;
							n = (( int )_buf[ 2 ] << 8) + _buf[ 3 ];
							tn += 2;
						}
						var tnn = tn + n;
						if (all < tnn)
							break;

						byte[] data = null;
						bool isText = b == 1;
						bool isClose = b == 8;
						if (!isText && !isClose && n > 0)
							data = new ArraySegment<byte>( _buf, tn, n ).Array;

						switch (b)
						{
							case 1: // text
								onString( _enc.GetString( _buf, tn, n ) );
								break;
							case 2: // bin
								onBinary( data );
								break;
							case 9: // ping
								if (!_isShuted)
									// send Pong
									send( 0xA, data, true );
								break;
							case 0xA: // pong
								if (!_isShuted)
									onPong( data );
								break;
							case 8: // close(protocol), here - shutdown
								{
									var code = ShutdownCode.Normal;
									if (n >= 2)
									{
										var bits = BitConverter.ToInt16( _buf, tn );
										bits = IPAddress.NetworkToHostOrder( bits );
										code = ( ShutdownCode )bits;
										data = n > 2 ? new ArraySegment<byte>( _buf, tn + 2, n - 2 ).Array : null;
									}
									// if this is not echos of our Shutdown...
									if (!_isShuted)
									{
										onShutdown( code, data );
										// send feedback: data = null
										Shutdown( code );
									}
									// and close connection
									Close();
								}
								break;
							default:
								{
									Helper.CheckArg( false, "Unknown WebSocket.Payload.Type {0}".Format( b ) );
								}
								break;
						}

						Array.Copy( _buf, tnn, _buf, 0, all -= tnn );
					}
				}

				if (!_isShuted && _stream == state.Item1)
					beginRecv( state.Item1, all );
			}
			catch (Exception err)
			{
				// if _stream is live (Close set _stream to null)
				if (_stream == state.Item1)
					onError( "recv", err );

				// we cant read data after errors: invalid buffer pointers and etc
				//Close();
				// !!! race-condition if somebody Open another connection
			}
		}

		#endregion

		#region handler callers
		void onClosed()
		{
			var cback = Handler;
			if (cback != null)
				try { cback.OnClosed( this ); }
				catch (Exception err) { Helper.RaiseUnexpected( err ); }
		}

		void onError( string op, Exception err )
		{
			var cback = Handler;
			if (cback != null)
				try { cback.OnError( this, op, err ); }
				catch (Exception err2) { Helper.RaiseUnexpected( err2 ); }
		}

		void onShutdown( ShutdownCode code, byte[] data )
		{
			var cback = Handler;
			if (cback != null)
				try { cback.OnShutdown( this, code, data ); }
				catch (Exception err) { Helper.RaiseUnexpected( err ); }
		}

		void onPong( byte[] data )
		{
			var cback = Handler;
			if (cback != null)
				try { cback.OnPong( this, data ); }
				catch (Exception err) { Helper.RaiseUnexpected( err ); }
		}

		void onString( string data )
		{
			var cback = Handler;
			if (cback != null)
				try { cback.OnString( this, data ); }
				catch (Exception err) { Helper.RaiseUnexpected( err ); }
		}

		void onBinary( byte[] data )
		{
			var cback = Handler;
			if (cback != null)
			{
				try { cback.OnBinary( this, data ); }
				catch (Exception err) { Helper.RaiseUnexpected( err ); }
			}
		}
		#endregion
	}

	//=============================================================================================
}
