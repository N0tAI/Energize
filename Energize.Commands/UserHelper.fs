﻿namespace Energize.Commands

module UserHelper =
    open Discord.WebSocket
    open Context
    open System
    open Discord
    open Energize.Commands.AsyncHelper

    let private rand = Random()

    let private tryFindGuildUser (ctx : CommandContext) (predicate : SocketGuildUser -> bool) =
        match ctx.isPrivate with
        | true -> None
        | false ->
            let res = ctx.guildUsers |> Seq.tryFind predicate
            match res with
            | Some user -> Some (user :> SocketUser)
            | None -> None

    let private handleMe (ctx : CommandContext) _ : SocketUser option =
        Some ctx.message.Author

    let private handleLast (ctx : CommandContext) _ : SocketUser option =
        match ctx.cache.lastMessage with
        | Some msg -> Some msg.Author
        | None -> None

    let private handleAdmin (ctx : CommandContext) _ : SocketUser option = 
        tryFindGuildUser ctx (fun user -> user.GuildPermissions.Administrator)
    
    let private handleRandom (ctx : CommandContext) _ : SocketUser option = 
        match ctx.isPrivate with
        | true -> None
        | false ->
            let len = (Seq.length ctx.guildUsers) 
            let i = rand.Next(0,len)
            Some (ctx.guildUsers.[i] :> SocketUser)
    
    let private handleId (ctx : CommandContext) (input : string option) : SocketUser option =
        match input with
        | Some arg ->
            try
                let id = uint64 arg
                tryFindGuildUser ctx (fun user -> user.Id.Equals(id))
            with _ -> None
        | None -> None

    let private handleRole (ctx : CommandContext) (input : string option) : SocketUser option =
        match input with
        | Some arg ->
            match ctx.isPrivate with
            | true -> None
            | false ->
                tryFindGuildUser ctx
                    (fun user -> user.Roles |> Seq.exists (fun role -> role.Name.Equals(arg)))
        | None -> None

    let private handles = [
        ("me", handleMe)
        ("last", handleLast)
        ("admin", handleAdmin)
        ("random", handleRandom)
        ("id", handleId)
        ("role", handleRole)
    ]

    let private findUserByTag (ctx : CommandContext) (tag : string) (arg : string option) : SocketUser option =
        let res = handles |> List.tryFind (fun (name, _) -> name.Equals(tag))
        match res with
        | Some (_, handle) -> handle ctx arg
        | None -> None

    let private getTagInfo (input : string) : (string * string option) =
        let parts = input.Split(' ') |> Seq.toList
        let identifier = parts.[0]
        let arg =
            match parts.Length > 1 with
            | true -> Some parts.[1]
            | false -> None 
        (identifier, arg)

    // welcome to the null hole
    let private matchesName (user : SocketGuildUser) (input : string) : bool =
        match user with
        | null -> false
        | user ->
            let name = 
                match user.Nickname with
                | null -> 
                    if user.Username.Equals(null) then 
                        String.Empty 
                    else 
                        user.Username
                | _ ->
                    user.Nickname
            name.ToLower().Contains(input.ToLower())

    let private findUserByMention (ctx : CommandContext) (input : string) : SocketUser option =
        match ctx.message.MentionedUsers |> Seq.length > 0 with
        | true ->
            ctx.message.MentionedUsers |> Seq.tryFind 
                (fun user -> 
                    input.Contains(@"<@" + user.Id.ToString() + ">") 
                    || input.Contains(@"<@!" + user.Id.ToString() + ">")
                )
        | false -> None

    let private findUserByName (ctx : CommandContext) (name : string) : SocketUser option =
        match ctx.isPrivate with
        | false ->
            match ctx.guildUsers |> List.tryFind (fun user -> matchesName user name) with
            | Some user -> Some (user :> SocketUser)
            | None -> None
        | true -> None

    let private findUserById (ctx : CommandContext) (input : string) (withId : bool) : SocketUser option =
        match withId with
        | true ->
            try
                let id = uint64 input
                match ctx.client.GetUser(id) with
                | null -> None
                | user -> Some user
            with _ -> None
        | false -> None

    let findUser (ctx : CommandContext) (input : string) (withId : bool) : SocketUser option =
        match input with
        | input when String.IsNullOrWhiteSpace input -> None 
        | input when input.StartsWith('$') && input |> String.length > 1 ->
            let (identifier, arg) = getTagInfo (input.Trim().[1..])
            findUserByTag ctx identifier arg
        | input ->
            match findUserByMention ctx input with
            | Some user -> Some user
            | None ->
                match findUserByName ctx input with
                | Some user -> Some user
                | None -> findUserById ctx input withId

    let getOrCreateRole (user : SocketGuildUser) (name : string) : IRole = 
        match user.Guild.Roles |> Seq.tryFind (fun role -> role.Name.Equals(name)) with
        | Some role -> role :> IRole
        | None -> awaitResult (user.Guild.CreateRoleAsync(name)) :> IRole

    let hasRole (user : SocketGuildUser) (name : string) = 
        match user.Roles |> Seq.tryFind (fun role -> role.Name.Equals(name)) with
        | Some _ -> true
        | None -> false

    let hasRoleStartingWith (user : SocketGuildUser) (input : string) = 
        match user.Roles |> Seq.tryFind (fun role -> role.Name.StartsWith(input)) with
        | Some _ -> true
        | None -> false