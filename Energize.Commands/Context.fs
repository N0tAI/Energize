﻿namespace Energize.Commands

module Context =
    open Discord.WebSocket
    open Discord
    open Discord.Rest
    open Energize.Toolkit
    open Cache
    open System
    open Energize.Interfaces.Services
    open AsyncHelper

    type CommandContext =
        {
            client : DiscordShardedClient
            restClient : DiscordRestClient
            message : SocketMessage
            arguments : string list
            prefix : string
            messageSender : MessageSender
            logger : Logger
            isPrivate : bool
            cache : CommandCache
            commandName : string
            serviceManager : IServiceManager
            random : Random
            guildUsers : SocketGuildUser list
            commandCount : int
        }

        member this.hasArguments =
            (this.arguments |> List.length > 0) && (not (String.IsNullOrWhiteSpace this.arguments.[0]))

        member this.input =
            String.Join(',', this.arguments).Trim()

        member this.authorMention =
            this.message.Author.Mention

        member this.sendOK (head : string option) (input : string) =
            let header = match head with Some h -> h | None -> this.commandName
            awaitIgnore (this.messageSender.Good(this.message, header, input))

        member this.sendWarn (head : string option) (input : string) =
            let header = match head with Some h -> h | None -> this.commandName
            awaitIgnore (this.messageSender.Warning(this.message, header, input))

        member this.sendBad (head : string option) (input : string) = 
            let header = match head with Some h -> h | None -> this.commandName
            awaitIgnore (this.messageSender.Danger(this.message, header, input))

    let isNSFW (msg : SocketMessage) (isPrivate : bool) = 
        if isPrivate then 
            true
        else
            let chan = msg.Channel :?> ITextChannel
            chan.IsNsfw || chan.Name.ToLower().Contains("nsfw")

    let isAuthorAdmin (msg : SocketMessage) (isPrivate : bool) =
        if isPrivate then
            true
        else
            let author = msg.Author :?> SocketGuildUser
            let roles = author.Roles |> Seq.filter (fun role -> role.Name.Equals("EnergizeAdmin") || role.Name.Equals("EBotAdmin"))
            (roles |> Seq.length > 0) || author.GuildPermissions.Administrator