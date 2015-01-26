using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.IO;
using System.Diagnostics;

using WebSockets.Utils;

// I dont use any json-lib for no dependencies
// sample shows using fastJSON with SocketIO

namespace WebSockets.Net
{
	//=============================================================================================

	// socket.io Event see https://github.com/LearnBoost/socket.io-spec #Messages 
    // https://github.com/automattic/socket.io-protocol
    // https://github.com/automattic/engine.io-protocol

	[Serializable]
	public enum EventType
	{
		unknown,

		message,
		connect,
		disconnect,
		open,
		close,
		error,
		retry,
		reconnect
	}

	[Serializable]
	public class EventData
	{
		public string name { get; set; }

		public EventType GetEventType()
		{
			var res = EventType.unknown;
			Enum.TryParse<EventType>( name, out res );
			return res;
		}

		public IList<object> arg { get; set; }
	}

	//=============================================================================================

	// I use events now 
	//		as Action<...> - no need to create some EventArgs(Windows.Forms) or RoutedEventsArgs(WPF)
	// usually u need OnString & OnJson events only, not all

	public class SocketIO : IDisposable, IWebSocketHandler
	{
		// not need attribute [beforeFieldInit]
		static SocketIO() { }

		public SocketIO() { }

		volatile WebSocket _sock; // = null
		public WebSocket WebSocket { get { return _sock; } }

		public void Open( string url ) { Open( url, Encoding.UTF8 ); }
		public void Open( string url, Encoding encoding )
		{
			// first close
			Close();

			var spt = parseResponse( url ); // url with session-id, ping, timeout
			if (spt == null)
				throw new NotSupportedException( "Server doesnt support websockets" );

			var ws = new WebSocket( encoding );
			ws.Handler = this;
			ws.IsMasking = true;
			ws.Open( spt.Item1 );
			_sock = ws;
		}

		public void Close()
		{
			var sock = Interlocked.Exchange( ref _sock, null );
			if (sock != null)
			{
				sock.Handler = null;
				sock.Close();
			}
		}

		public void Dispose()
		{
			Close();
		}

		#region events
		// see https://github.com/LearnBoost/socket.io-spec #Messages

		volatile Action<string, int?, string> _onMessage;
		public event Action<string, int?, string> OnMessage // data, message-id, endPoint
		{
			add { _onMessage += value; }
			remove { _onMessage += value; }
		}
		
		volatile Action<string, int?, string> _onJson;
		public event Action<string, int?, string> OnJson // data, message-id, endPoint
		{
			add { _onJson += value; }
			remove { _onJson += value; }
		}

		public event Action<string> OnDisconnect; // endPoint
		public event Action<string, string> OnConnect; // path, query
		public event Action<string, int?, string> OnEvent; // json-event, message-id, endPoint
		public event Action<int, string> OnAck; // message-id, data
		public event Action<string, string, string> OnError; // endPoint, reason + advise

		public event Action<int?, string, string> OnHeartbeat; // message-id, endPoint, data
		public event Action<int?, string, string> OnNoop; // message-id, endPoint, data

		#endregion events

		#region protocol packets
		// see https://github.com/LearnBoost/socket.io-spec #Messages

		public void Disconnect( string endPoint = "" )
		{
			_sock.Send( string.Format( "0::{0}", endPoint ) );
			_wasDisconnect = true;
			Close(); // ?
		}

		public void Connect( string path = "", string query = "" )
		{
			_sock.Send( string.Format( "1::{0}{1}", path, query ) );
		}

		public void Heartbeat()
		{
			_sock.SendAscii( "2::" );
		}
		public void Heartbeat( int? messageId = null, string data = "", string endPoint = "" )
		{
			data = string.IsNullOrEmpty( data ) ? "" : ":" + data;
			_sock.Send( string.Format( "2:{0}:{1}{2}",
				messageId.HasValue ? messageId.Value.ToString() : "", endPoint, data ) );
		}

		public void Message( string message, int? messageId = null, string endPoint = "" )
		{
			_sock.Send( string.Format( "3:{0}:{1}:{2}",
				messageId.HasValue ? messageId.Value.ToString() : "", endPoint, message ) );
		}

		public void Json( string data, int? messageId = null, string endPoint = "" )
		{
			_sock.Send( string.Format( "4:{0}:{1}:{2}",
				messageId.HasValue ? messageId.Value.ToString() : "", endPoint, data ) );
		}

		// u can use EventData type (to string)
		public void Event( string data, int? messageId = null, string endPoint = "" )
		{
			_sock.Send( string.Format( "5:{0}:{1}:{2}",
				messageId.HasValue ? messageId.Value.ToString() : "", endPoint, data ) );
		}

		public void Ack( int messageId, string data = "" )
		{
			data = string.IsNullOrEmpty( data ) ? "" : ":" + data;
			_sock.Send( string.Format( "6:{0}{1}", messageId, data ) );
		}

		public void Error( string reason, string advise = "", string endPoint = "" )
		{
			advise = string.IsNullOrEmpty( advise ) ? "" : "+" + advise;
			_sock.Send( string.Format( "7::{0}:{1}{2}", endPoint, reason, advise ) );
		}

		public void Noop()
		{
			_sock.SendAscii( "8::" );
		}
		public void Noop( int? messageId = null, string data = "", string endPoint = "" )
		{
			data = string.IsNullOrEmpty( data ) ? "" : ":" + data;
			_sock.Send( string.Format( "8:{0}:{1}{2}",
				messageId.HasValue ? messageId.Value.ToString() : "", endPoint, data ) );
		}
		#endregion protocol packets

		#region IWebSocketHandler
		void IWebSocketHandler.OnString( WebSocket sender, string data )
		{
			handleString( data );
		}

		void IWebSocketHandler.OnBinary( WebSocket sender, byte[] data )
		{
		}

		void IWebSocketHandler.OnPong( WebSocket sender, byte[] data )
		{
		}

		void IWebSocketHandler.OnError( WebSocket sender, string op, Exception err )
		{
			var cback = OnError;
			if (cback != null)
				try { cback( null, err.Message, op ); }
				catch (Exception err2) { Helper.RaiseUnexpected( err2 ); }
		}

		void IWebSocketHandler.OnShutdown( WebSocket sender, ShutdownCode code, byte[] data )
		{
			// ignore ?
		}

		volatile bool _wasDisconnect = false;
		void IWebSocketHandler.OnClosed( WebSocket sender )
		{
			if (_wasDisconnect)
				return;

			var cback = OnDisconnect;
			if (cback != null)
				try { cback( null ); }
				catch (Exception err) { Helper.RaiseUnexpected( err ); }
			_wasDisconnect = true;
		}
		#endregion IWebSocketHandler

		#region implementation
		void handleString( string data )
		{
			try
			{
				Helper.Debug( "DATA: {0}", data );
				char ch = data[ 0 ];
				switch (ch)
				{
					case '0':
						{
							// Action<string> OnDisconnect; // endPoint
							var cback = OnDisconnect;
							if (cback != null)
								cback( data.Length > 3 ? data.Substring( 3 ) : null );
						}
						break;
					case '1':
						// Action<string, string> OnConnect; // path, query
						{
							data = data.Substring( 3 );
							var pos = data.IndexOf( '?' ) + 1;

							string query = null;
							if (0 < pos) // query exists
							{
								query = data.Substring( pos );
								data = data.Substring( 0, pos - 1 );
							}

							var cback = OnConnect;
							if (cback != null)
								cback( data, query );
						}
						break;
					case '2':
						// Action<int?, string, string> OnHeartbeat; // message-id, endPoint, data
						//$TEMP just echo
						_sock.SendAscii( data );
						Helper.Debug( "PING: {0}", data );
						break;
					case '3':
						// Action<string, int?, string> OnMessage; // data, message-id, endPoint
						{
							var pos1 = 2; //data.IndexOf( ':' );
							var pos2 = data.IndexOf( ':', pos1 ) + 1;
							var pos3 = data.IndexOf( ':', pos2 ) + 1;

							int? mid = null;
							if (pos1 + 1 < pos2) // message-id exists
								// no new strings and ignores non-digitals
								mid = ( int )Helper.Int64FromString( data, pos1, 100 );

							string endPoint = null;
							if (pos2 + 1 < pos3) // endPoint exists
								endPoint = data.Substring( pos2, pos3 - pos2 + 1 );

							data = data.Substring( pos3 );

							var cback = _onMessage;
							if (cback != null)
								cback( data, mid, endPoint );
						}
						break;
					case '4':
						// Action<string, int?, string> OnJson; // data, message-id, endPoint
						{
							var pos1 = 2; //data.IndexOf( ':' );
							var pos2 = data.IndexOf( ':', pos1 ) + 1;
							var pos3 = data.IndexOf( ':', pos2 ) + 1;

							int? mid = null;
							if (pos1 + 1 < pos2) // message-id exists
								// no new strings and ignores non-digitals
								mid = ( int )Helper.Int64FromString( data, pos1, 100 );

							string endPoint = null;
							if (pos2 + 1 < pos3) // endPoint exists
								endPoint = data.Substring( pos2, pos3 - pos2 + 1 );

							data = data.Substring( pos3 );

							var cback = _onJson;
							if (cback != null)
								cback( data, mid, endPoint );
						}
						break;
					case '5':
						// Action<string, int?, string> OnEvent; // json-event, message-id, endPoint
						break;
					case '6':
						// Action<int, string> OnAck; // message-id, data
						break;
					case '7':
						// Action<string, string, string> OnError; // endPoint, reason + advise
						break;
					case '8':
						// Action<int?, string, string> OnNoop; // message-id, endPoint, data
						//$TEMP just echo
						_sock.SendAscii( data );
						break;
					default:
						Helper.RaiseUnexpected( new NotSupportedException( "Unknown socket.io packet " + ch ) );
						break;
				}
			}
			catch (Exception err)
			{
				Helper.RaiseUnexpected( err );
			}
		}

		static readonly DateTime Era1970 = new DateTime( 1970, 1, 1 );
		static readonly char[] RespDelims = { ',', ' ', '.', ';' };
		// returns url to WS with SessionID, Ping and Timeout
		private Tuple<string, int, int> parseResponse( string url )
		{
            string handshakeParams = "";
			var nurl = url;

            if (url.IndexOf('?') != -1) // if params are given
            {
                var qIndex = url.IndexOf('?');
                handshakeParams = url.Substring(qIndex + 1);
                url = url.Substring(0, qIndex);
            }

			// if doesnt contains socket.io specific..
			if (url.IndexOf( "socket.io/1" ) < 0)
				nurl = string.Format( "{0}/socket.io/1/", url );

			// add timestamp
			if (handshakeParams.Length == 0) // no params
                nurl += string.Format("/?t={0}", (long)(DateTime.UtcNow - Era1970).TotalMilliseconds);
            else
				nurl += string.Format("/?{0}&t={1}", handshakeParams, (long)(DateTime.UtcNow - Era1970).TotalMilliseconds);

			nurl = nurl.Replace( "//", "/" );
			nurl = nurl.Replace( ":/", "://" );
			Debug.Print( "URL = [{0}]", nurl );

			var req = ( HttpWebRequest )WebRequest.Create( new Uri( nurl ) );
			req.Method = "POST";

			// vars for see it in Debugger
			var resp = req.GetResponse();

			string text = null;
			using (var tin = new StreamReader( resp.GetResponseStream() ))
				text = tin.ReadToEnd();
			Debug.Print( "RESP = [{0}]", text );

			// no support for websocket
			if (string.IsNullOrEmpty( text ))
				return null;
			var pos = text.IndexOf( "websocket" );
			if (pos < 0)
				return null;

			var beg = text.LastIndexOfAny( RespDelims, pos );
			beg = beg < 0 ? 0 : beg + 1;
			var end = text.IndexOfAny( RespDelims, pos );
			end = end < 0 ? text.Length - 1 : end - 1;
			var spt = text.Substring( beg, end - beg + 1 );

			spt = spt.Replace( ":websocket", "" ).Replace( "websocket:", "" ).Replace( "websocket", "" );
			var vals = spt.Split( new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries );

			int ping = 15, timeout = 25;
			if (vals == null || vals.Length == 0)
				return null;
			if (vals.Length > 1)
				Int32.TryParse( vals[ 1 ], out ping );
			if (vals.Length > 2)
				Int32.TryParse( vals[ 2 ], out timeout );
			else
				timeout = ping + 10;

			nurl = nurl.Replace( "https:", "wss:" ).Replace( "http:", "ws:" );
			pos = nurl.LastIndexOf( '/' );
			nurl = nurl.Substring( 0, pos );
			nurl = string.Format( "{0}/websocket/{1}", nurl, vals[ 0 ] );
			Debug.Print( "WS = [{0}]", nurl );

			return new Tuple<string, int, int>( nurl, ping, timeout );
		}
		#endregion implementation
	}
}
