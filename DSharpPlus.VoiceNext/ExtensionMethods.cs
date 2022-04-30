// This file is part of the DSharpPlus project.
//
// Copyright (c) 2015 Mike Santiago
// Copyright (c) 2016-2022 DSharpPlus Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using DSharpPlus.Entities;

namespace DSharpPlus.VoiceNext
{
    /// <summary>
    /// Defines various extensions specific to CommandsNext.
    /// </summary>
    public static class ExtensionMethods
    {
        public static VoiceNextExtension UseVoiceNext(this DiscordClient client, VoiceNextConfiguration? configuration = null)
        {
            if (client.GetExtension<VoiceNextExtension>() != null)
            {
                throw new InvalidOperationException("VoiceNext is already enabled on this client.");
            }
            else if (!client.Intents.HasIntent(DiscordIntents.GuildVoiceStates))
            {
                throw new InvalidOperationException("VoiceNext requires the GuildVoiceStates intent.");
            }

            var voiceNextExtension = new VoiceNextExtension(configuration);
            client.AddExtension(voiceNextExtension);
            return voiceNextExtension;
        }

        public static async Task<IReadOnlyDictionary<int, VoiceNextExtension>> UseVoiceNextAsync(this DiscordShardedClient shardedClient, VoiceNextConfiguration? configuration = null)
        {
            var shards = new Dictionary<int, VoiceNextExtension>();
            await shardedClient.InitializeShardsAsync().ConfigureAwait(false);

            for (var i = 0; i < shardedClient.ShardClients.Count; i++)
            {
                var voiceNextExtension = shardedClient.ShardClients[i].GetExtension<VoiceNextExtension>();
                if (voiceNextExtension == null)
                {
                    voiceNextExtension = shardedClient.ShardClients[i].UseVoiceNext(configuration);
                }

                shards[shardedClient.ShardClients[i].ShardId] = voiceNextExtension;
            }

            return new ReadOnlyDictionary<int, VoiceNextExtension>(shards);
        }

        public static VoiceNextExtension GetVoiceNext(this DiscordClient client) => client.GetExtension<VoiceNextExtension>();

        public static VoiceNextExtension GetVoiceNext(this DiscordShardedClient shardedClient, int shardId) => shardId < 0 || shardId >= shardedClient.ShardClients.Count
            ? throw new ArgumentOutOfRangeException(nameof(shardId))
            : shardedClient.ShardClients[shardId].GetExtension<VoiceNextExtension>();

        public static VoiceNextExtension GetVoiceNext(this DiscordShardedClient shardedClient, DiscordGuild guild) => guild == null ? throw new ArgumentNullException(nameof(guild)) : shardedClient.GetShard(guild.Id).GetVoiceNext();

        public static async Task<IReadOnlyDictionary<int, VoiceNextExtension>> GetVoiceNextAsync(this DiscordShardedClient shardedClient)
        {
            await shardedClient.InitializeShardsAsync().ConfigureAwait(false);
            var shards = new Dictionary<int, VoiceNextExtension>();
            for (var i = 0; i < shardedClient.ShardClients.Count; i++)
            {
                var voiceNextExtension = shardedClient.ShardClients[i].GetExtension<VoiceNextExtension>();
                shards[shardedClient.ShardClients[i].ShardId] = voiceNextExtension;
            }

            return new ReadOnlyDictionary<int, VoiceNextExtension>(shards);
        }
    }
}
