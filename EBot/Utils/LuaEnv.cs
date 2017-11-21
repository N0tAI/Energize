﻿using Discord;
using Discord.Rest;
using Discord.WebSocket;
using EBot.Logs;
using NLua;
using NLua.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace EBot.Utils
{
    class LuaEnv
    {
        private static string _Path = "External/Lua/SavedScripts";
        private static RestApplication _App;
        private static Dictionary<ulong, Lua> _States = new Dictionary<ulong, Lua>();
        private static string ScriptSeparator = "\n-- GEN --\n";

        public static async Task Initialize(EBotClient client)
        {
            _App = await client.Discord.GetApplicationInfoAsync();
            
            try
            {
                if (!Directory.Exists(_Path))
                {
                    Directory.CreateDirectory(_Path);
                }
            }
            catch (Exception ex)
            {
                BotLog.Debug(ex.Message);
            }

            if (!Directory.Exists(_Path))
            {
                Directory.CreateDirectory(_Path);
            }

            foreach (string filepath in Directory.GetFiles(_Path))
            {
                string path = filepath.Replace(@"\", "/");
                string[] dir = path.Split("/");
                string id = dir[dir.Length - 1];
                id = id.Remove(id.Length - 4);
                ulong chanid = ulong.Parse(id);

                Lua state = CreateState(chanid);
                string script = File.ReadAllText(path);
                string[] parts = script.Split(ScriptSeparator,StringSplitOptions.RemoveEmptyEntries);
                foreach(string part in parts)
                {
                    state["PART"] = part.Trim();
                    state.DoString(@"sandbox(PART)");
                    state["PART"] = null;
                }
            }

            client.Discord.UserJoined += async user =>
            {
                IReadOnlyList<SocketGuildChannel> channels = user.Guild.Channels as IReadOnlyList<SocketGuildChannel>;
                foreach(SocketGuildChannel chan in channels)
                {
                    if (_States.ContainsKey(chan.Id))
                    {
                        Lua state = _States[chan.Id];
                        state["USER"] = user as SocketUser;
                        Object[] returns = state.DoString(@"return event.fire('OnMemberJoined',USER)");
                        state["USER"] = null;
                        await client.Handler.EmbedReply.Send((chan as ISocketMessageChannel), "Lua Event", returns[0].ToString());
                    }
                }
            };

            client.Discord.UserLeft += async user =>
            {
                IReadOnlyList<SocketGuildChannel> channels = user.Guild.Channels as IReadOnlyList<SocketGuildChannel>;
                foreach (SocketGuildChannel chan in channels)
                {
                    if (_States.ContainsKey(chan.Id))
                    {
                        Lua state = _States[chan.Id];
                        state["USER"] = user as SocketUser;
                        Object[] returns = state.DoString(@"return event.fire('OnMemberLeft',USER)");
                        state["USER"] = null;
                        await client.Handler.EmbedReply.Send((chan as ISocketMessageChannel), "Lua Event", returns[0].ToString());
                    }

                }
            };

            client.Discord.MessageReceived += async msg =>
            {
                if (_States.ContainsKey(msg.Channel.Id) && msg.Author.Id != _App.Id)
                {
                    Lua state = _States[msg.Channel.Id];
                    state["USER"] = msg.Author;
                    state["MESSAGE"] = msg;
                    Object[] returns = state.DoString(@"return event.fire('OnMessageCreated',USER,MESSAGE)");
                    state["USER"] = null;
                    state["MESSAGE"] = null;
                    await client.Handler.EmbedReply.Send((msg.Channel as ISocketMessageChannel), "Lua Event", returns[0].ToString());
                }
            };

            client.Discord.MessageDeleted += async (msg, c) =>
            {
                IMessage mess = await msg.GetOrDownloadAsync();
                if (_States.ContainsKey(mess.Channel.Id) && mess.Author.Id != _App.Id)
                {
                    Lua state = _States[c.Id];
                    state["MESSAGE"] = mess as SocketMessage;
                    state["USER"] = mess.Author as SocketUser;
                    Object[] returns = state.DoString(@"return event.fire('OnMessageDeleted',USER,MESSAGE)");
                    state["USER"] = null;
                    state["MESSAGE"] = null;
                    await client.Handler.EmbedReply.Send((mess.Channel as ISocketMessageChannel), "Lua Event", returns[0].ToString());
                }
            };

            client.Discord.MessageUpdated += async (cache, msg, c) =>
            {
                if (_States.ContainsKey(c.Id) && msg.Author.Id != _App.Id)
                {
                    Lua state = _States[c.Id];
                    state["USER"] = msg.Author as SocketUser;
                    state["MESSAGE"] = msg as SocketMessage;
                    Object[] returns = state.DoString(@"return event.fire('OnMessageEdited',USER,MESSAGE)");
                    state["USER"] = null;
                    state["MESSAGE"] = null;
                    await client.Handler.EmbedReply.Send(c, "Lua Event", returns[0].ToString());
                }
            };
        }

        private static string SafeCode(Lua state,string code)
        {
            state["UNTRUSTED_CODE"] = code;
            code = code.TrimStart();
            return @"local result = sandbox(UNTRUSTED_CODE)
                if result.Success then
                    if result.PrintStack ~= '' then
                        return result.PrintStack,unpack(result.Varargs)
                    else
                        return unpack(result.Varargs)
                    end
                else
                    error(result.Error,0)
                end";
        }

        private static Lua CreateState(ulong chanid)
        {
            Lua state = new Lua();
            string sandbox = File.ReadAllText("./External/Lua/Init.lua");
            state.DoString(sandbox);

            return state;
        }

        private static void Save(SocketChannel chan,string code)
        {
            code = code.TrimStart();
            string path =  _Path + "/" + chan.Id + ".lua";
            File.AppendAllText(path,code + ScriptSeparator);
        }

        public static bool Run(SocketMessage msg,string code,out List<Object> returns,out string error,BotLog log)
        {
            SocketChannel chan = msg.Channel as SocketChannel;
            if (!_States.ContainsKey(chan.Id) || _States[chan.Id] == null)
            {
                _States[chan.Id] = CreateState(chan.Id);
            }

            Save(chan, code);

            bool success = true;

            try
            {
                Lua state = _States[chan.Id];
                code = SafeCode(state,code);

                Object[] parts = state.DoString(code,"SANDBOX");
                returns = new List<object>(parts);
                error = "";
            }
            catch(LuaException e)
            {
                returns = new List<object>();
                error = e.Message;
                success = false;
            }
            catch(Exception e) //no lua return basically
            {
                returns = new List<object>();
                error = e.Message;
                success = true;
            }

            return success;
        }

        public static void Reset(ulong chanid)
        {
            if (_States.ContainsKey(chanid))
            {
                _States[chanid].DoString("collectgarbage()");
                _States[chanid].Close();
                _States[chanid].Dispose();
                string path = _Path + "/" + chanid + ".lua";
                File.Delete(path);
            }
            _States[chanid] = null;
        }
    }
}
