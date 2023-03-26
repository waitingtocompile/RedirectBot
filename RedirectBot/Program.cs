using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;

public class AppConfig
{
	public string? token;

	public List<ProtectedChannelGroup>? protectedChannelGroups;
}

public class ProtectedChannelGroup
{
	public ulong roleID { get; private set; }
	public string moveMessage { get; private set; }

	[JsonConstructor]
	public ProtectedChannelGroup(ulong roleID, string moveMessage)
	{
		this.roleID = roleID;
		this.moveMessage = moveMessage;
	}
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

	private DiscordSocketClient _client;

	public Dictionary<ulong, string> roleProtectedMoveMessages { get; private set; }

	Program()
	{
		_client = new DiscordSocketClient();
		_client.Log += Log;
		_client.Ready += RefreshCommands;
		_client.SlashCommandExecuted += ExecuteMoveCommand;

		roleProtectedMoveMessages = new Dictionary<ulong, string>();
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

		if(config.protectedChannelGroups != null)
		{
			foreach(var group in config.protectedChannelGroups)
			{
				roleProtectedMoveMessages.Add(group.roleID, group.moveMessage);
			}
		}

		await _client.LoginAsync(TokenType.Bot, config.token);
		await _client.StartAsync();

		//our work is done, block and let the client run off in worker threads
		await Task.Delay(-1);
	}

	public async Task RefreshCommands()
	{
		if((await _client.GetGlobalApplicationCommandsAsync()).Any(command=> command.Name == "move"))
		{
			// we have already registered the command, no need to do so again.
			// if we ever need to add command versioning, this will need to be updated
			return;
		}

		var commandBuilder = new SlashCommandBuilder();

		commandBuilder.WithName("move")
			.WithDescription("Send discussion to another channel")
			.AddOption("target-channel",
				ApplicationCommandOptionType.Channel,
				"The channel to send discussion to",
				isRequired:true,
				channelTypes:new List<ChannelType> { ChannelType.Text })
			.WithDMPermission(false);

		try
		{
			var command = await _client.CreateGlobalApplicationCommandAsync(commandBuilder.Build());

			await Log("registered the move slash command");
		}
		catch(Exception ex) 
		{
			await Log(ex.Message);
		}
	}

	public async Task ExecuteMoveCommand(SocketSlashCommand command)
	{
		if(command.CommandName != "move")
		{
			//not our job to respond to it. This shouldn't come up though, this is the only command we offer
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

		await Log($"performed move from {originChannel.Id} to {targetChannel.Id}, at the request of {command.User.Id}");

		await command.RespondAsync("Moving the discussion now...", ephemeral:true);

		var destinationMessage = await targetChannel.SendMessageAsync(embed: ComposeMoveFromMessage(originChannel, null, command.User));
		var originMessage = await originChannel.SendMessageAsync(embed:ComposeMoveToMessage(targetChannel, destinationMessage, command.User));

		await destinationMessage.ModifyAsync(messageProperties => messageProperties.Embed = ComposeMoveFromMessage(originChannel, originMessage, command.User));
	}

	public List<string> FindMoveMessages(SocketTextChannel channel)
	{
		List<string> messages = new List<string>();

		List<ulong> inheritingIds = new List<ulong>();

		foreach(var permissionOverwrite in channel.PermissionOverwrites)
		{
			if (permissionOverwrite.TargetType == PermissionTarget.Role)
			{


				if (permissionOverwrite.Permissions.ViewChannel == PermValue.Allow)
				{
					var message = roleProtectedMoveMessages.GetValueOrDefault(permissionOverwrite.TargetId);
					if (message != null)
					{
						messages.Add(message);
					}
				}
				else if (permissionOverwrite.Permissions.ViewChannel == PermValue.Inherit)
				{
					inheritingIds.Add(permissionOverwrite.TargetId);
				}
			}
		}

		if(inheritingIds.Count == 0)
		{
			return messages;
		}

		foreach(var permissionOverwrite in channel.Category.PermissionOverwrites)
		{
			if(inheritingIds.Contains(permissionOverwrite.TargetId) && permissionOverwrite.Permissions.ViewChannel == PermValue.Allow)
			{
				var message = roleProtectedMoveMessages.GetValueOrDefault(permissionOverwrite.TargetId);
				if (message != null)
				{
					messages.Add(message);
				}
			}
		}

		return messages;
	}

	public Embed ComposeMoveToMessage(SocketTextChannel targetChannel, RestUserMessage destinationMessage, SocketUser movedBy)
	{
		EmbedBuilder builder = new EmbedBuilder();
		builder.WithAuthor(movedBy)
			.WithColor(Color.Teal)
			.WithTitle("Discussion Moved")
			.WithDescription($"Moving the discussion to {targetChannel.Mention}\n" +
			$"Carry it on at {destinationMessage.GetJumpUrl()}");

		foreach(var extraMessage in FindMoveMessages(targetChannel))
		{
			builder.Description += $"\n\n{extraMessage}";
		}
		
		return builder.Build();
	}

	public Embed ComposeMoveFromMessage(SocketTextChannel originChannel, RestUserMessage? originMessage, SocketUser movedBy)
	{
		EmbedBuilder builder = new EmbedBuilder();
		builder.WithAuthor(movedBy)
			.WithColor(Color.Teal)
			.WithTitle("Discussion Moved Here")
			.WithDescription($"Continuing the discussion from {originChannel.Mention}\n" +
			(originMessage==null ? "" : $"See the earlier discussion at {originMessage.GetJumpUrl()}"));

		foreach (var extraMessage in FindMoveMessages(originChannel))
		{
			builder.Description += $"\n\n{extraMessage}";
		}

		return builder.Build();
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
