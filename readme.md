# RedirectBot
This is a very simple discord bot for redirecting converations from one channel to another. It provides links in both directions, so that people looking over past discussions can hop betwen both halves of the discussion can easily.

This bot was originally designed for use on the #include C++ server

# Building, configuring and running

## Building
This bot targets .NET 6.0, and requires Discord.NET 3.9.0. It has no other unnusual build requirements

## Required Discord Permissions
This bot requires the "slash command" and "send messages" permissions (2147491840).

## Configuration
This bot is configured with a `app_config.json` file, which must be placed in the application's working directory. It has the following options
 - `token` - a valid security token for the bot user you want the bot to operate as
 - `protectedChannelGroups` - a list describing how the bot should behave around channels that have their visibility restricted behind role permissions. Each entry contains the following values
   - `roleID` - the numeric ID of the role that controls channel access
   - `moveMessage` - a message to be displayed in the movement message when moving to or from that channel

