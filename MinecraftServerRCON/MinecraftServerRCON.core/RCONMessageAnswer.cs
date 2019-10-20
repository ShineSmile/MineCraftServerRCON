using System;
using System.Text;

namespace MinecraftServerRCON
{
	internal sealed class RCONMessageAnswer
	{
		public static readonly RCONMessageAnswer EMPTY = new RCONMessageAnswer(false, new byte[] {});
		
		private bool success = false;
		private string answer = string.Empty;
		private byte[] data = null;
		private int responseId = -1;
		
		public RCONMessageAnswer(bool success, byte[] data, int responseId = -1)
		{
			this.success = success;
			this.data = data;
			this.responseId = responseId;
		}
		
		public byte[] Data
		{
			get
			{
				return this.data;
			}
		}
		
		public bool Success
		{
			get
			{
				return this.success;
			}
		}
		
		public string Answer
		{
			get
			{
				return ASCIIEncoding.UTF8.GetString(this.data);
			}
		}
		
		public int ResponseId
		{
			get
			{
				return this.responseId;
			}
		}
	}
}
