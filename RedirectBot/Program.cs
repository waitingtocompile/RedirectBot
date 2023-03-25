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
				program.Log("Configuration file \"app_config.json\" not found");
				return Task.CompletedTask;
			}

			return program.RunAsync(JsonConvert.DeserializeObject<AppConfig>(config_file));
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Unhandled top-level exception in Application: {ex.Message}");
			return Task.CompletedTask;
		}
	}

	public DiscordSocketClient _client { get; private set; }

	Program()
	{
		_client = new DiscordSocketClient();
		_client.Log += Log;
		_client.Ready += OnClientReady;
		_client.SlashCommandExecuted += OnMoveCommandExecuted;
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
		await _client.StartAsync();

		//just block for now
		await Task.Delay(-1);
	}

	public async Task OnClientReady()
	{
		//this doesn't actually need to be done every time it's run, only when the command is new or changing

		var commandBuilder = new SlashCommandBuilder();


		commandBuilder.WithName("move")
			.WithDescription("Send discussion to another channel")
			.AddOption("target-channel",
				ApplicationCommandOptionType.Channel,
				"The channel to send discussion to",
				isRequired:true,
				channelTypes:new List<ChannelType> { ChannelType.Text });

		try
		{
			await _client.CreateGlobalApplicationCommandAsync(commandBuilder.Build());

			await Log("registered the move slash command");
		}
		catch(Exception ex) 
		{
			await Log(ex.Message);
		}
	}

	public async Task OnMoveCommandExecuted(SocketSlashCommand command)
	{
		if(command.CommandName != "move")
		{
			//not our job to respond to it
			return;
		}

		if(command.Channel.GetChannelType() != ChannelType.Text && command.Channel.GetChannelType() != ChannelType.PublicThread)
		{
			await command.RespondAsync("This command can only be used in text channels and public threads", ephemeral: true);
			return;
		}

		SocketTextChannel? targetChannel = null;
		SocketTextChannel originChannel = (SocketTextChannel)command.Channel;

        foreach (var option in command.Data.Options)
        {
			if(option.Name == "target-channel")
			{
				targetChannel = (SocketTextChannel)option.Value;
			}
        }
		if(targetChannel == null)
		{
			
			await command.RespondAsync("An internal error occurred, contact this bot's maintainer", ephemeral:true);
			await Log("move command had no target channel offered");
			return;
		}

		//log that the command happened

		if (targetChannel.Id == command.Channel.Id)
		{
			await command.RespondAsync("You can't move this discussion to a channel it's already in", ephemeral:true);
			return;
		}

		await command.RespondAsync("Moving the discussion now...", ephemeral:true);

		var destinationMessage = await targetChannel.SendMessageAsync($"Continuing the discussion from <#{originChannel.Id}>");
		var originMessage = await originChannel.SendMessageAsync($"This discussion has moved to <#{targetChannel.Id}> (moved by <@{command.User.Id}>)\n" +
			$"Carry it on at {destinationMessage.GetJumpUrl()}");

		await destinationMessage.ModifyAsync(messageProperties => messageProperties.Content = $"Continuing the discussion from <#{originChannel.Id}>\n" +
		$"Carried on from {originMessage.GetJumpUrl()}");
	}

	private Task Log(LogMessage msg)
	{
		return Log(msg.ToString());
	}

	private Task Log(string msg)
	{
		Console.WriteLine(msg);
		return Task.CompletedTask;
	}

}