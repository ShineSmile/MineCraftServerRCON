using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Net.Sockets;

namespace MinecraftServerRCON
{
	public sealed class RCONClient : IDisposable
	{
		private static readonly int timeoutSeconds = 10;
		private static readonly byte[] PADDING = new byte[] {0x0, 0x0};
		public static readonly RCONClient INSTANCE = new RCONClient();
		
		private bool isInit = false;
		private string server = string.Empty;
		private string password = string.Empty;
		private int port = 25575;
		private int messageCounter = 0;
		private NetworkStream stream = null;
		private TcpClient tcp = null;
		private BinaryWriter writer = null;
		private BinaryReader reader = null;
		private ReaderWriterLockSlim threadLock = new ReaderWriterLockSlim();
		private RCONReader rconReader = RCONReader.INSTANCE;
		
		private RCONClient()
		{
			this.isInit = false;
		}
		
		public void setupStream(string minecraftServer, int port = 25575, string password = "")
		{
			if(this.isInit)
			{
				return;
			}
			
			this.server = minecraftServer;
			this.port = port;
			this.password = password;
			this.openConnection();
		}
		
		private void openConnection()
		{
			try
			{
				this.tcp = new TcpClient(this.server, this.port);
				this.stream = this.tcp.GetStream();
				this.writer = new BinaryWriter(this.stream);
				this.reader = new BinaryReader(this.stream);
				this.rconReader.setup(this.reader);
				
				if(this.password != string.Empty)
				{
					var answer = this.internalSendMessage(RCONMessageType.Login, this.password);
					if(answer == RCONMessageAnswer.EMPTY)
					{
						this.isInit = false;
						return;
					}
				}
				
				this.isInit = true;
			}
			catch
			{
				this.isInit = false;
			}
		}
		
		public string sendMessage(RCONMessageType type, string command)
		{
			if(this.isInit == false)
			{
				return string.Empty;
			}
			
			return this.internalSendMessage(type, command).Answer;
		}
		
		private RCONMessageAnswer internalSendMessage(RCONMessageType type, string command)
		{
			this.threadLock.EnterWriteLock();
			
			try
			{
				var messageNumber = ++this.messageCounter;
				
				var msg = new List<byte>();
				msg.AddRange(BitConverter.GetBytes(10 + command.Length));
				msg.AddRange(BitConverter.GetBytes(messageNumber));
				msg.AddRange(BitConverter.GetBytes(type.Value));
				msg.AddRange(ASCIIEncoding.UTF8.GetBytes(command));
				msg.AddRange(PADDING);
				
				this.writer.Write(msg.ToArray());
				this.writer.Flush();
				this.threadLock.ExitWriteLock();
				
				var sendTime = DateTime.UtcNow;
				while (true)
				{
					var answer = this.rconReader.getAnswer(messageNumber);
					if(answer == RCONMessageAnswer.EMPTY)
					{
						if((DateTime.UtcNow - sendTime).TotalSeconds > timeoutSeconds)
						{
							return RCONMessageAnswer.EMPTY;
						}
						
						Thread.Sleep(100);
						continue;
					}
					
					return answer;
				}
			}
			catch
			{
				return RCONMessageAnswer.EMPTY;
			}
			finally
			{
				try
				{
					this.threadLock.ExitWriteLock();
				}
				catch
				{
				}
			}
		}

		#region IDisposable implementation

		public void Dispose()
		{
			this.isInit = false;
			this.rconReader.Dispose();
			
			if(this.writer != null)
			{
				try
				{
					this.writer.Dispose();
				}
				catch
				{
				}
			}
			
			if(this.reader != null)
			{
				try
				{
					this.reader.Dispose();
				}
				catch
				{
				}
			}
			
			if(this.stream != null)
			{
				try
				{
					this.stream.Dispose();
				}
				catch
				{
				}
			}
			
			if(this.tcp != null)
			{
				try
				{
					this.tcp.Close();
				}
				catch
				{
				}
			}
		}

		#endregion
	}
}
