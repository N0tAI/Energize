﻿namespace Energize.Commands.Implementation

open Energize.Commands.Command

[<CommandModule("Utils")>]
module Util =
    open Energize.Commands.AsyncHelper
    open System.Diagnostics
    open Energize.Commands.Context
    open System
    open Energize.Interfaces.Services
    open Energize.Toolkit
    open Discord
    open System.Threading.Tasks
    open System.Text
    open Microsoft.Data.Sqlite

    [<Command("ping", "ping <nothing>", "Pings the bot")>]
    let ping (ctx : CommandContext) = async {
        let timestamp = ctx.message.Timestamp
        let diff = timestamp.Millisecond / 10
        let res = sprintf "⏰ Discord: %dms\n🕐 Bot: %dms" diff ctx.client.Latency
        ctx.sendOK None res
    }

    [<Command("mem", "mem <nothing>", "Gets the current memory usage")>]
    let mem (ctx : CommandContext) = async {
        let proc = Process.GetCurrentProcess()
        let mbused = proc.WorkingSet64 / 1024L / 1024L
        ctx.sendOK None (sprintf "Currently using %dMB of memory" mbused)
    }    

    [<Command("uptime", "uptime <nothing>", "Gets the current uptime")>]
    let uptime (ctx : CommandContext) = async {
        let diff = (DateTime.Now - Process.GetCurrentProcess().StartTime).Duration();
        let res = sprintf "%dd%dh%dm" diff.Days diff.Hours diff.Minutes
        ctx.sendOK None ("The current instance has been up for " + res)
    }

    [<CommandParameters(1)>]
    [<Command("say", "Makes me say something", "say <sentence>")>]
    let say (ctx : CommandContext) = async {
        ctx.sendOK None ctx.input
    }

    [<CommandParameters(1)>]
    [<Command("l", "Runs your lua code in a sandbox", "l <luastring>")>]
    let lua (ctx : CommandContext) = async {
        let env = ctx.serviceManager.GetService<ILuaService>("Lua")
        let returns : Collections.Generic.List<obj> ref = ref (Collections.Generic.List<obj>())
        let error = ref String.Empty
        if env.Run(ctx.message, ctx.input, returns, error, ctx.logger) then
            let display = String.Join('\t', returns.contents)
            if String.IsNullOrWhiteSpace display then
                ctx.sendOK (Some "lua") "👌 (nil or no value was returned)"
            else
                if display |> String.length > 2000 then
                    ctx.sendWarn (Some "lua") "Output was too long to be sent"
                else
                    ctx.sendWarn (Some "lua") display
        else
            ctx.sendBad (Some "lua") (sprintf "```\n%s```" (error.contents.Replace("`","")))
    }

    [<Command("lr", "Reset the channel's lua environment", "lr <nothing>")>]
    let luaReset (ctx : CommandContext) = async {
        let env = ctx.serviceManager.GetService<ILuaService>("Lua")
        env.Reset(ctx.message.Channel.Id, ctx.logger)
        ctx.sendOK (Some "lua") "Lua environment reset in this channel"
    }

    [<CommandParameters(1)>]
    [<Command("feedback", "Send feedback to the owner (suggestion, bug, etc...)", "feedback <sentence>")>]
    let feedback (ctx : CommandContext) = async {
        let sender = ctx.serviceManager.GetService<IWebhookSenderService>("Webhook")
        let feedback = ctx.input
        let name = ctx.message.Author.Username
        let avatar = ctx.message.Author.GetAvatarUrl(ImageFormat.Auto)
        let chan = ctx.client.GetChannel(Config.FEEDBACK_CHANNEL_ID)
        let log = 
            if ctx.isPrivate then
                let c = ctx.message.Channel :?> IGuildChannel
                sprintf "%s#%s" c.Guild.Name c.Name
            else
                ctx.message.Author.ToString()

        ctx.sendOK None "Successfully sent your feedback"

        let builder = EmbedBuilder()
        builder
            .WithDescription(feedback)
            .WithTimestamp(ctx.message
            .CreatedAt).WithFooter(log)
            |> ignore

        match chan :> IChannel with
        | :? ITextChannel as textChan ->
            awaitIgnore (sender.SendEmbed(textChan, builder.Build(), name, avatar))
        | _ ->
            ctx.logger.Warning("Feedback channel wasnt a text channel?!")
    }

    [<OwnerOnlyCommand>]
    [<CommandParameters(1)>]
    [<Command("to", "Timing out test", "to <seconds>")>]
    let timeOut (ctx : CommandContext) = async {
        let duration = int ctx.input
        await (Task.Delay(duration * 1000))
        ctx.sendOK (Some "Time Out") (sprintf "Timed out during `%d`s" duration)
    }

    [<CommandParameters(1)>]
    [<Command("b64e", "Encodes a sentence to base64", "b64e <sentence>")>]
    let base64Encode (ctx : CommandContext) = async {
        let bytes = Encoding.UTF8.GetBytes(ctx.input)
        let res = Convert.ToBase64String(bytes)

        if res |> String.length > 2000 then
            ctx.sendWarn None "Output too long to be sent"
        else
            ctx.sendOK None res
    }

    [<CommandParameters(1)>]
    [<Command("b64d", "Decodes a sentence to base64", "b64d <sentence>")>]
    let base64Decode (ctx : CommandContext) = async {
        let bytes = Convert.FromBase64String(ctx.input);
        let res = Encoding.UTF8.GetString(bytes);

        if res |> String.length > 2000 then
            ctx.sendWarn None "Output too long to be sent"
        else
            ctx.sendOK None res
    }

    [<OwnerOnlyCommand>]
    [<CommandParameters(1)>]
    [<Command("sql", "Runs an sql statement in the database", "sql <sqlstring>")>]
    let sql (ctx : CommandContext) = async {
        let conn = new SqliteConnection(Config.DB_CONNECTION_STRING)
        try
            await (conn.OpenAsync())
            let cmd = new SqliteCommand(ctx.input, conn)
            let reader = cmd.ExecuteReader()
            if not (reader.HasRows) then
                ctx.sendWarn None "No data was gathered for the specified statement"
            else
                let builder = StringBuilder()
                while reader.Read() do
                    let values = 
                        seq { for i in 0 .. reader.FieldCount - 1 -> reader.[i].ToString() }
                        |> Seq.toList
                    builder.Append(sprintf "%s\n%s\n" (String.Join('\t', values)) (String('-', 50))) 
                        |> ignore
                let res = sprintf "```\n%s\n%s```" (String('-', 50)) (builder.ToString())
                if res |> String.length > 2000 then
                    ctx.sendWarn None "Output too long to be sent"
                else
                    ctx.sendOK None res
        with ex ->
            ctx.sendBad None ("```\n" + ex.Message.Replace("`", "") + "```")
    }

    [<OwnerOnlyCommand>]
    [<Command("err", "Throws an error for testing", "err <nothing|message>")>]
    let err (ctx : CommandContext) = async {
        let msg = if ctx.hasArguments then ctx.input else "test"
        raise (Exception(msg))
    }

    [<OwnerOnlyCommand>]
    [<CommandParameters(1)>]
    [<Command("ev", "Evals a C# string", "ev <csharpstring>")>]
    let eval (ctx : CommandContext) = async {
        let evaluator = ctx.serviceManager.GetService<ICSharpEvaluatorService>("Evaluator")
        let res = awaitResult (evaluator.Eval(ctx.input, ctx))

        match res.ToTuple() with
        | (0, out) -> ctx.sendBad None out
        | (1, out) -> ctx.sendOK None out
        | (_, out) -> ctx.sendWarn None out
    }