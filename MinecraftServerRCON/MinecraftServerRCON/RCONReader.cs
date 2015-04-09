using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;

namespace MinecraftServerRCON
{
	internal class RCONReader : IDisposable
	{
		public static readonly RCONReader INSTANCE = new RCONReader();
		
		private bool isInit = false;
		private BinaryReader reader = null;
		private ConcurrentBag<RCONMessageAnswer> answers = new ConcurrentBag<RCONMessageAnswer>();
		
		private RCONReader()
		{
			this.isInit = false;
		}
		
		public void setup(BinaryReader reader)
		{
			this.reader = reader;
			this.isInit = true;
			this.readerThread();
		}
		
		public RCONMessageAnswer getAnswer(int messageId)
		{
			var matching = this.answers.Where(n => n.ResponseId == messageId).ToList();
			var data = new List<byte>();
			var dummy = RCONMessageAnswer.EMPTY;
			
			if(matching.Count > 0)
			{
				matching.ForEach(n => { data.AddRange(n.Data); this.answers.TryTake(out dummy);});
				return new RCONMessageAnswer(true, data.ToArray(), messageId);
			}
			else
			{
				return RCONMessageAnswer.EMPTY;
			}
		}
		
		private void readerThread()
		{
			new Thread(() => 
			{
			    Thread.CurrentThread.IsBackground = true; 
			    while(true)
			    {
			    	if(this.isInit == false)
			    	{
			    		return;
			    	}
			    	
			    	try
			    	{
			    		var len = this.reader.ReadInt32();
			    		var reqId = this.reader.ReadInt32();
			    		var type = this.reader.ReadInt32();
			    		var data = len > 10 ? this.reader.ReadBytes(len - 10): new byte[] { };
			    		var pad = this.reader.ReadBytes(2);
			    		var msg = new RCONMessageAnswer(reqId > -1, data, reqId);
			    		this.answers.Add(msg);
			    	}
			    	catch(EndOfStreamException e)
			    	{
			    		return;
			    	}
			    	catch(ObjectDisposedException e)
			    	{
			    		return;
			    	}
			    	catch
			    	{
			    		return;
			    	}
			    	
			    	Thread.Sleep(100);
			    }
			}).Start();
		}
		
		#region IDisposable implementation
		public void Dispose()
		{
			this.isInit = false;
		}
		#endregion
	}
}
