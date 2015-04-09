# MinecraftServerRCONSharp
A thread safe Minecraft server's RCON implementation for C# and Mono.

Example usage: Change the "ABC" player's game mode
```C#
  using System;
  using MinecraftServerRCON;
  
  namespace RCONTest
  {
	class Program
	{
		var answer = string.Empty;
		using(var rcon = RCONClient.INSTANCE)
		{
	    		rcon.setupStream("127.0.0.1", password: "123");
	    		answer = rcon.sendMessage(RCONMessageType.Command, "gamemode creative ABC");
	    		Console.WriteLine(answer.RemoveColorCodes());
		}
    	}
  }
```
