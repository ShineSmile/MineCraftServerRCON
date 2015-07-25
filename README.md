# MinecraftServerRCONSharp
A thread-safe Minecraft server's RCON implementation for C# and Mono.

Example usage: Change the "ABC" player's game mode
```C#
  using System;
  using MinecraftServerRCON;
  
  namespace RCONTest
  {
	class Program
	{
		using(var rcon = RCONClient.INSTANCE)
		{
	    		rcon.setupStream("127.0.0.1", password: "123");
	    		answer = rcon.sendMessage(RCONMessageType.Command, "gamemode creative ABC");
	    		Console.WriteLine(answer.RemoveColorCodes());
		}
	}
  }
```

## Setup
The fasted way to use this library is to use NuGet: https://www.nuget.org/packages/RCONServer/. Further, you can also download the library from the [releases](https://github.com/SommerEngineering/MinecraftServerRCONSharp/releases) page.