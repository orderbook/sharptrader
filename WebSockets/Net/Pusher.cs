using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.IO;
using System.Diagnostics;

using WebSockets.Utils;
using Newtonsoft.Json;

namespace WebSockets.Net
{
	//=============================================================================================

    [Serializable]
    public class PusherEventWrapper
    {
        public string @event { get; set; }
        public string data { get; set; }
    }

    [Serializable]
    public class PusherConnectionEstablished
    {
        public string socket_id { get; set; }
        public long activity_timeout { get; set; }
    }

    [Serializable]
    public class PusherSubscribe
    {
        public string channel { get; set; }
    }

	// I use events now 
	//		as Action<...> - no need to create some EventArgs(Windows.Forms) or RoutedEventsArgs(WPF)
	// usually u need OnString & OnJson events only, not all

	public class Pusher : IDisposable, IWebSocketHandler
	{
		// not need attribute [beforeFieldInit]
		static Pusher() { }

        public Pusher() { }

		volatile WebSocket _sock; // = null
		public WebSocket WebSocket { get { return _sock; } }

		public void Open( string url ) { Open( url, Encoding.UTF8 ); }
		public void Open( string url, Encoding encoding )
		{
			// first close
			Close();

			/*var spt = parseResponse( url ); // url with session-id, ping, timeout
			if (spt == null)
				throw new NotSupportedException( "Server doesnt support websockets" );*/

			var ws = new WebSocket( encoding );
			ws.Handler = this;
			ws.Open( url );
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

        public void Subscribe(string ch)
        {
            PusherEventWrapper pev = new PusherEventWrapper();
            PusherSubscribe ps = new PusherSubscribe();

            pev.@event = "pusher:subscribe";
            ps.channel = ch;
            pev.data = JsonConvert.SerializeObject(ps);

            //_sock.SendUTF8(JsonConvert.SerializeObject(pev));
            //string str = "{\"event\":\"pusher:subscribe\",\"data\":\"{\\\"channel\\\":\\\"order_book\\\"}\"}";
            string str = "{\"event\":\"pusher:subscribe\",\"data\":{\"channel\":\"order_book\"}}";
            _sock.Send(str);
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
        public event Action<PusherConnectionEstablished> OnConnect; // data
		public event Action<string, int?, string> OnEvent; // json-event, message-id, endPoint
		public event Action<int, string> OnAck; // message-id, data
		public event Action<string, string, string> OnError; // endPoint, reason + advise

		public event Action<int?, string, string> OnHeartbeat; // message-id, endPoint, data
		public event Action<int?, string, string> OnNoop; // message-id, endPoint, data

		#endregion events

		#region protocol packets
        // see http://pusher.com/docs/pusher_protocol

		public void Disconnect( string endPoint = "" )
		{
			//_sock.Send( string.Format( "0::{0}", endPoint ) );
			_wasDisconnect = true;
			Close(); // ?
		}

		public void Connect( string path = "", string query = "" )
		{
			//_sock.Send( string.Format( "1::{0}{1}", path, query ) );
		}

		public void Heartbeat()
		{
			//_sock.SendAscii( "2::" );
		}
		public void Heartbeat( int? messageId = null, string data = "", string endPoint = "" )
		{
			data = string.IsNullOrEmpty( data ) ? "" : ":" + data;
			//_sock.Send( string.Format( "2:{0}:{1}{2}",
			//	messageId.HasValue ? messageId.Value.ToString() : "", endPoint, data ) );
		}

		public void Message( string message, int? messageId = null, string endPoint = "" )
		{
			//_sock.Send( string.Format( "3:{0}:{1}:{2}",
			//	messageId.HasValue ? messageId.Value.ToString() : "", endPoint, message ) );
		}

		public void Json( string data, int? messageId = null, string endPoint = "" )
		{
			//_sock.Send( string.Format( "4:{0}:{1}:{2}",
			//	messageId.HasValue ? messageId.Value.ToString() : "", endPoint, data ) );
		}

		// u can use EventData type (to string)
		public void Event( string data, int? messageId = null, string endPoint = "" )
		{
			//_sock.Send( string.Format( "5:{0}:{1}:{2}",
			//	messageId.HasValue ? messageId.Value.ToString() : "", endPoint, data ) );
		}

		public void Ack( int messageId, string data = "" )
		{
			//data = string.IsNullOrEmpty( data ) ? "" : ":" + data;
			//_sock.Send( string.Format( "6:{0}{1}", messageId, data ) );
		}

		public void Error( string reason, string advise = "", string endPoint = "" )
		{
			//advise = string.IsNullOrEmpty( advise ) ? "" : "+" + advise;
			//_sock.Send( string.Format( "7::{0}:{1}{2}", endPoint, reason, advise ) );
		}

		public void Noop()
		{
			//_sock.SendAscii( "8::" );
		}
		public void Noop( int? messageId = null, string data = "", string endPoint = "" )
		{
			//data = string.IsNullOrEmpty( data ) ? "" : ":" + data;
			//_sock.Send( string.Format( "8:{0}:{1}{2}",
			//	messageId.HasValue ? messageId.Value.ToString() : "", endPoint, data ) );
		}
		#endregion protocol packets

		#region IWebSocketHandler
		void IWebSocketHandler.OnString( WebSocket sender, string data )
		{
			handlePusherEvent( data );
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
        void handlePusherEvent(string msgStr)
		{
			try
			{
                var eventWrap = JsonConvert.DeserializeObject<PusherEventWrapper>(msgStr);

                switch (eventWrap.@event)
				{
                    case "pusher:connection_established":
						// Action<string, string> OnConnect; // path, query
						{
                            var data = JsonConvert.DeserializeObject<PusherConnectionEstablished>(eventWrap.data);

							var cback = OnConnect;
							if (cback != null)
								cback( data );
						}
						break;

                    case "pusher_internal:subscription_succeeded":
                        {
                            //msgStr	"{\"event\":\"\",\"data\":\"{}\",\"channel\":\"order_book\"}"	string
                            // Ignore for now
                        }
                        break;

                    case "data":
                        {
                            //msgStr	"{\"event\":\"data\",\"data\":\"{}\",\"channel\":\"order_book\"}"	string
                            // Ignore for now
                        }
                        break;


					default:
						Helper.RaiseUnexpected( new NotSupportedException( "Unknown socket.io packet " + eventWrap.@event ) );
						break;
				}
			}
			catch (Exception err)
			{
				Helper.RaiseUnexpected( err );
			}
		}

		#endregion implementation
	}
}
