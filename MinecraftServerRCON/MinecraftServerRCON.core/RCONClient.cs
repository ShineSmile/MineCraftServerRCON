using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MinecraftServerRCON
{
	public sealed class RCONClient : IDisposable
	{
		// Current servers like e.g. Spigot are not able to work async :(
		private static readonly bool rconServerIsMultiThreaded = false;

		private static readonly int timeoutSeconds = 3;
		private static readonly byte[] PADDING = new byte[] { 0x0, 0x0 };
		public static readonly RCONClient INSTANCE = new RCONClient();

		private bool isInit = false;
		private bool isConfigured = false;
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
			this.isConfigured = false;
		}

		public void setupStream(string minecraftServer, int port = 25575, string password = "")
		{
			this.threadLock.EnterWriteLock();

			try
			{
				if (this.isConfigured)
				{
					return;
				}

				this.server = minecraftServer;
				this.port = port;
				this.password = password;
				this.isConfigured = true;
				this.openConnection();
			}
			finally
			{
				this.threadLock.ExitWriteLock();
			}
		}

		private void openConnection()
		{
			if (this.isInit)
			{
				return;
			}

			try
			{
				this.rconReader = RCONReader.INSTANCE;
				this.tcp = new TcpClient(this.server, this.port);
				this.stream = this.tcp.GetStream();
				this.writer = new BinaryWriter(this.stream);
				this.reader = new BinaryReader(this.stream);
				this.rconReader.setup(this.reader);

				if (this.password != string.Empty)
				{
					var answer = this.internalSendAuth();
					if (answer == RCONMessageAnswer.EMPTY)
					{
						this.isInit = false;
						throw new Exception("IPAddress or Password error!");
					}
				}

				this.isInit = true;
			}
			catch (Exception e)
			{
				this.isInit = false;
				this.isConfigured = false;
				throw e;
			}
			finally
			{
				// To prevent huge CPU load if many reconnects happens.
				// Does not effect any normal case ;-)
				Thread.Sleep(TimeSpan.FromSeconds(0.1));
			}
		}

		public string sendMessage(RCONMessageType type, string command)
		{
			if (!this.isConfigured)
			{
				return RCONMessageAnswer.EMPTY.Answer;
			}

			return this.internalSendMessage(type, command).Answer;
		}

		public void fireAndForgetMessage(RCONMessageType type, string command)
		{
			if (!this.isConfigured)
			{
				return;
			}

			this.internalSendMessage(type, command, true);
		}

		private RCONMessageAnswer internalSendAuth()
		{
			// Build the message:
			var command = this.password;
			var type = RCONMessageType.Login;
			var messageNumber = ++this.messageCounter;
			var msg = new List<byte>();
			msg.AddRange(BitConverter.GetBytes(10 + Encoding.UTF8.GetByteCount(command)));
            msg.AddRange(BitConverter.GetBytes(messageNumber));
			msg.AddRange(BitConverter.GetBytes(type.Value));
			msg.AddRange(ASCIIEncoding.UTF8.GetBytes(command));
			msg.AddRange(PADDING);

			// Write the message to the wire:
			this.writer.Write(msg.ToArray());
			this.writer.Flush();

			return waitReadMessage(messageNumber);
		}

		private RCONMessageAnswer internalSendMessage(RCONMessageType type, string command, bool fireAndForget = false)
		{
			try
			{
				var messageNumber = 0;

				try
				{
					this.threadLock.EnterWriteLock();

					// Is a reconnection necessary?
					if (!this.isInit || this.tcp == null || !this.tcp.Connected)
					{
						this.internalDispose();
						this.openConnection();
					}


                    // Build the message:
                    messageNumber = ++this.messageCounter;
					var msg = new List<byte>();
					msg.AddRange(BitConverter.GetBytes(10 + Encoding.UTF8.GetByteCount(command)));
					msg.AddRange(BitConverter.GetBytes(messageNumber));
					msg.AddRange(BitConverter.GetBytes(type.Value));
					msg.AddRange(Encoding.UTF8.GetBytes(command));
					msg.AddRange(PADDING);

					// Write the message to the wire:
					this.writer.Write(msg.ToArray());
					this.writer.Flush();
				}
				finally
				{
					this.threadLock.ExitWriteLock();
				}

				if (fireAndForget && rconServerIsMultiThreaded)
				{
					var id = messageNumber;
					Task.Factory.StartNew(() =>
					{
						waitReadMessage(id);
					});

					return RCONMessageAnswer.EMPTY;
				}

				return waitReadMessage(messageNumber);
			}
			catch (Exception e)
			{
				Console.WriteLine("Exception while sending: " + e.Message);
				return RCONMessageAnswer.EMPTY;
			}
		}

		private RCONMessageAnswer waitReadMessage(int messageNo)
		{
			var sendTime = DateTime.UtcNow;
			while (true)
			{
				var answer = this.rconReader.getAnswer(messageNo);
				if (answer == RCONMessageAnswer.EMPTY)
				{
					if ((DateTime.UtcNow - sendTime).TotalSeconds > timeoutSeconds)
					{
						return RCONMessageAnswer.EMPTY;
					}

					Thread.Sleep(TimeSpan.FromSeconds(0.001));
					continue;
				}

				return answer;
			}
		}

		#region IDisposable implementation

		public void Dispose()
		{
			this.threadLock.EnterWriteLock();

			try
			{
				this.internalDispose();
			}
			finally
			{
				this.threadLock.ExitWriteLock();
			}
		}

		#endregion

		private void internalDispose()
		{
			this.isInit = false;

			try
			{
				this.rconReader.Dispose();
			}
			catch
			{
			}

			if (this.writer != null)
			{
				try
				{
					this.writer.Dispose();
				}
				catch
				{
				}
			}

			if (this.reader != null)
			{
				try
				{
					this.reader.Dispose();
				}
				catch
				{
				}
			}

			if (this.stream != null)
			{
				try
				{
					this.stream.Dispose();
				}
				catch
				{
				}
			}

			if (this.tcp != null)
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
	}
}
