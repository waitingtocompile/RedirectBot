using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

public class AppConfig
{
	public string? token;
}

public class Program
{
	public static Task Main(string[] args)
	{
		try
		{
			Program program = new Program();

			var config_file = File.ReadAllText("app_config.json");
			if(config_file == null)
			{
				Console.WriteLine("Configuration file \"app_config.json\" not found");
				return Task.CompletedTask;
			}

			return program.RunAsync(JsonConvert.DeserializeObject<AppConfig>(config_file));
		}
		catch (Exception ex)
		{
			Console.WriteLine("Unhandled top-level exception in Application: " + ex.Message);
			return Task.CompletedTask;
		}
	}

	public DiscordSocketClient _client { get; private set; }

	Program()
	{
		_client = new DiscordSocketClient();
		_client.Log += Log;
	}

	public async Task RunAsync(AppConfig? config)
	{
		if(config == null)
		{
			throw new ArgumentNullException("Application configuration was not parsed correctly, check its format");
		}

		if (string.IsNullOrEmpty(config.token))
		{
			throw new ArgumentNullException("Discord API key was absent or empy, check the application configuration file");
		}

		await _client.LoginAsync(TokenType.Bot, config.token);
		await _client.StopAsync();

		//just block for now
		await Task.Delay(-1);
	}


	private Task Log(LogMessage msg)
	{
		Console.WriteLine(msg.ToString());
		return Task.CompletedTask;
	}

	
}