﻿using DSharpPlus;
using System;
using System.Collections.Generic;
using System.Text;

namespace EBot.Logs
{
    class SpyLog
    {
        private DiscordClient _Client;
        private BotLog _Log;
        
        public DiscordClient Client { get => this._Client; set => this._Client = value; }
        public BotLog Log { get => this._Log; set => this._Log = value; }

        public void WatchWords(string[] tospy)
        {
            this._Client.MessageCreated += async e =>
            {
                string content = e.Message.Content;
                for (int i = 0; i < tospy.Length; i++)
                {
                    string used = tospy[i];
                    if (content.ToLower().Contains(used))
                    {
                        this._Log.Warning("[SpyLog] >> " + e.Message.Author.Username + ": " + content);
                    }
                }
            };
        }
    }
}
