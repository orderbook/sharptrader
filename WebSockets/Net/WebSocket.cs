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

    internal enum Fin : byte
    {
        More = 0x0,
        Final = 0x1
    }

    internal enum Rsv : byte
    {
        Off = 0x0,
        On = 0x1
    }

    internal enum Mask : byte
    {
        Unmask = 0x0,
        Mask = 0x1
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

    /// <summary>
    /// Contains the values of the opcode that indicates the type of a WebSocket frame.
    /// </summary>
    /// <remarks>
    /// The values of the opcode are defined in
    /// <see href="http://tools.ietf.org/html/rfc6455#section-5.2">Section 5.2</see> of RFC 6455.
    /// </remarks>
    public enum Opcode : byte
    {
        /// <summary>
        /// Equivalent to numeric value 0.
        /// Indicates a continuation frame.
        /// </summary>
        Cont = 0x0,
        /// <summary>
        /// Equivalent to numeric value 1.
        /// Indicates a text frame.
        /// </summary>
        Text = 0x1,
        /// <summary>
        /// Equivalent to numeric value 2.
        /// Indicates a binary frame.
        /// </summary>
        Binary = 0x2,
        /// <summary>
        /// Equivalent to numeric value 8.
        /// Indicates a connection close frame.
        /// </summary>
        Close = 0x8,
        /// <summary>
        /// Equivalent to numeric value 9.
        /// Indicates a ping frame.
        /// </summary>
        Ping = 0x9,
        /// <summary>
        /// Equivalent to numeric value 10.
        /// Indicates a pong frame.
        /// </summary>
        Pong = 0xa
    }

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
			send( Opcode.Ping, data, true );
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
			send( Opcode.Close, data, true );
		}

		// known exactly length of byte buffer
		public void SendAscii( string str, bool flush = true )
		{
			send( str, Encoding.ASCII, str.Length, flush );
		}

        public void SendUTF8(string str, bool flush = true)
        {
            send(str, Encoding.UTF8, str.Length, flush);
        }

		public void Send( string str, bool flush = true )
		{
			var len = _enc.GetByteCount( str );
			send( str, _enc, len, flush );
		}

		public void Send( byte[] data, bool flush = true )
		{
			Helper.CheckArgNotNull( data, "data" );
			send( Opcode.Binary, data, flush );
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
			int len = Encoding.UTF8.GetBytes( sreq, 0, sreq.Length, _buf_data, 0 );
			stream.Write( _buf_data, 0, len );

			// read response from server
			whs = readHttpResponse( stream, ResponseHeaders );
			// Sincerely yours, Captain Obviousness

			// check headers
			if ((!"HTTP/1.1 101 Switching Protocols".Equals(whs[HttpKey]) && !"HTTP/1.1 101 Web Socket Protocol Handshake".Equals(whs[HttpKey]))
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
			beginRecv( stream );
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
				_buf_data[ pos++ ] = b;

				eof = (eof << 8) + b;
				if (eof == EOF)
					return whs;

				if ((eof & 0xffff) == EOL)
				{
					// UTF8. ignore _enc
                    var line = Encoding.UTF8.GetString(_buf_data, 0, pos - 2);
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

        readonly byte[] _header = new byte[10];
		readonly byte[] _buf_data = new byte[ 65 * 1024 ];
		bool beginRecv( Stream stream )
		{
			try
			{
                int pos = 0;
				var state = new Tuple<Stream, int>( stream, pos );
                stream.BeginRead(_header, 0, 2, recv, state);
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
				var ofs = prepareFrame( Opcode.Text, len );
				enc.GetBytes( str, 0, str.Length, buf, ofs );
				maskFrame( len, ofs );
				send( buf, len + ofs, flush );
			}
		}

		private void send( Opcode payload, byte[] data, bool flush )
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
				_isShuted = _isShuted || (payload == Opcode.Close);
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
		int prepareFrame(Opcode payload, int len)
		{
			//var len = data != null ? data.Length : 0;
			Helper.CheckArg( len < c64K, "length must be less 64K. NB: string.Length <= UTF8(string).Length for non-ASCII" );
			Helper.CheckArg( ( uint )payload < 16, "unknow data's payload type" );

			var ofs = 2;

			var buf = _sbuf;
			buf[ 0 ] = (byte)((int)payload & 0x0F);
			buf[ 0 ] |= 0x80; // FIN

			// length
			if (len < 126)
			{
				buf[ 1 ] = ( byte )len;
			}
			else if (len < 0x010000)
			{
				buf[ 1 ] = 126;
				short ulen = IPAddress.HostToNetworkOrder( (short)len );
				buf[ 2 ] = ( byte )(ulen & 0xff);
				buf[ 3 ] = ( byte )(ulen >> 8);

				ofs += 2;
			}
			else
			{
				buf[1] = 127;

				long ulen = IPAddress.HostToNetworkOrder((long)len);

				buf[2] = (byte)(ulen & 0xff);
				buf[3] = (byte)(ulen >> 8);
				buf[4] = (byte)(ulen >> 16);
				buf[5] = (byte)(ulen >> 24);
				buf[6] = (byte)(ulen >> 32);
				buf[7] = (byte)(ulen >> 40);
				buf[8] = (byte)(ulen >> 48);
				buf[9] = (byte)(ulen >> 56);

				ofs += 8;
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

			var buf = _sbuf;
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

        public static void readBytes(Stream stream, byte[] data, int initialOffset, int dataLen)
        {
            int offset = initialOffset;
            int remaining = dataLen;
            while (remaining > 0)
            {
                int read = stream.Read(data, offset, remaining);
                if (read <= 0)
                    throw new EndOfStreamException
                        (String.Format("End of stream reached with {0} bytes left to read", remaining));
                remaining -= read;
                offset += read;
            }
        }

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

        void recv(IAsyncResult ar)
        {
            var state = ar.AsyncState as Tuple<Stream, int>;
            var stream = state.Item1;
            try
            {
                int len = stream.EndRead(ar);
                int offs = 0;

                if (len == 0)
                {
                    onClosed();
                    return;
                }

                if (len < 2)
                {
                    // Read up the remaining byte!
                    readBytes(stream, _header, 1, 1);
                }

                /* Parse the frame header:
                 * 1 byte - flags and opcode
                 * 1 byte - mask and payload length
                 * 0 - 8 bytes - payload length
                 * payload
                */
                var fin = (_header[0] & 0x80) == 0x80 ? Fin.Final : Fin.More; // FIN
                var rsv1 = (_header[0] & 0x40) == 0x40 ? Rsv.On : Rsv.Off; // RSV1
                var rsv2 = (_header[0] & 0x20) == 0x20 ? Rsv.On : Rsv.Off; // RSV2
                var rsv3 = (_header[0] & 0x10) == 0x10 ? Rsv.On : Rsv.Off; // RSV3
                var opcode = (Opcode)(_header[0] & 0x0f); // Opcode
                var mask = (_header[1] & 0x80) == 0x80 ? Mask.Mask : Mask.Unmask; // MASK
                int payloadLen = (byte)(_header[1] & 0x7f); // Payload Length

                // no masking from server
                Helper.CheckArg(mask == Mask.Unmask, "Unexpected WebSocket.frame-masking from server");

                // Calculate extended payload length size
                int extPayloadBytes = payloadLen < 126 ? 0 : payloadLen == 126 ? 2 : 8;

                // Read it from the stream
                if (extPayloadBytes > 0)
                    readBytes(stream, _header, 2, extPayloadBytes);

                // Decode payloadLen (if it's more than 126)
                if (payloadLen >= 126)
                {
                    if (payloadLen == 126)
                    {
                        payloadLen = ((int)_header[2] << 8) + _header[3];
                    }
                    else
                    {
                        payloadLen = (int)(((long)_header[2] << 56) +
                                             ((long)_header[3] << 48) +
                                             ((long)_header[4] << 40) +
                                             ((long)_header[5] << 32) +
                                             ((long)_header[6] << 24) +
                                             ((long)_header[7] << 16) +
                                             ((long)_header[8] << 8) +
                                             _header[9]);

                        //var len64 = BitConverter.ToInt64(_buf, 2);
                        //len64 = IPAddress.NetworkToHostOrder(bits);

                        //if (payloadLen >= Int32.MaxValue) throw new System.ArgumentOutOfRangeException();
                    }
                }

                byte[] data = null;

                // Read the payload, if any
                if (payloadLen > 0)
                {
                    // Choose the buffer
                    if (payloadLen <= _buf_data.Length)
                        data = _buf_data;
                    else
                        data = new byte[payloadLen];

                    // Read the data into it
                    readBytes(stream, data, 0, (int)payloadLen);
                }

                // Process the frame now
                if (fin == Fin.Final)
                {
                    switch (opcode)
                    {
                        case Opcode.Text: // text
                            onString(_enc.GetString(data, 0, payloadLen));
                            break;
                        case Opcode.Binary: // bin
                            onBinary(data);
                            break;
                        case Opcode.Ping: // ping
                            if (!_isShuted)
                                // send Pong
                                send(Opcode.Pong, data, true);
                            break;
                        case Opcode.Pong: // pong
                            if (!_isShuted)
                                onPong(data);
                            break;
                        case Opcode.Close: // close(protocol), here - shutdown
                            {
                                var code = ShutdownCode.Normal;
                                byte[] closeData = null;
                                if (payloadLen >= 2)
                                {
                                    var bits = BitConverter.ToInt16(data, 0);
                                    bits = IPAddress.NetworkToHostOrder(bits);
                                    code = (ShutdownCode)bits;

                                    // Pass close data, if any
                                    if (payloadLen > 2)
                                    {
                                        closeData = new byte[payloadLen - 2];
                                        Array.Copy(data, 2, closeData, 0, closeData.Length);
                                    }
                                }
                                // if this is not echos of our Shutdown...
                                if (!_isShuted)
                                {
                                    onShutdown(code, data);
                                    // send feedback: data = null
                                    Shutdown(code);
                                }
                                // and close connection
                                Close();
                            }
                            break;
                        default:
                            {
                                Helper.CheckArg(false, "Unknown WebSocket.Payload.Type {0}".Format(opcode));
                            }
                            break;
                    }
                }
                else
                {
                    //Helper.CheckArg(false, "This WebSocket\'s implementation doesnt support non-FIN frames");
                }

                // Read next messages header
                if (!_isShuted && _stream == stream)
                    beginRecv(stream);
            }
            catch (Exception err)
            {
                // if _stream is live (Close set _stream to null)
                if (_stream == stream)
                    onError("recv", err);

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
