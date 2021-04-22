﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Steeltoe.Discovery.Eureka.Transport;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Steeltoe.Discovery.Eureka.AppInfo
{
    public class Application
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        public int Count => InstanceMap.Count;

        [JsonPropertyName("instance")]
        [JsonConverter(typeof(JsonInstanceInfoConverter))]
        public IList<InstanceInfo> Instances
        {
            get
            {
                return new List<InstanceInfo>(InstanceMap.Values);
            }

            set
            {
                foreach (var instanceInfo in value)
                {
                    InstanceMap.AddOrUpdate(
                        instanceInfo.InstanceId,
                        instanceInfo,
                        (key, newInstanceInfo) =>
                        {
                            return newInstanceInfo;
                        });
                }
            }
        }

        internal ConcurrentDictionary<string, InstanceInfo> InstanceMap { get; } = new ConcurrentDictionary<string, InstanceInfo>();

        public InstanceInfo GetInstance(string instanceId)
        {
            InstanceMap.TryGetValue(instanceId, out var result);
            return result;
        }

        public override string ToString()
        {
            var sb = new StringBuilder("Application[");
            sb.Append("Name=" + Name);
            sb.Append(",Instances=");
            foreach (var inst in Instances)
            {
                sb.Append(inst.ToString());
                sb.Append(",");
            }

            sb.Append("]");
            return sb.ToString();
        }

        public Application()
        {
        }

        internal Application(string name)
        {
            Name = name;
        }

        internal Application(string name, IList<InstanceInfo> instances)
        {
            Name = name;
            foreach (var info in instances)
            {
                Add(info);
            }
        }

        internal void Add(InstanceInfo info)
        {
            if (!string.IsNullOrEmpty(info.InstanceId))
            {
                InstanceMap[info.InstanceId] = info;
            }
            else if (!string.IsNullOrEmpty(info.HostName))
            {
                InstanceMap[info.HostName] = info;
            }
        }

        internal void Remove(InstanceInfo info)
        {
            if (!InstanceMap.TryRemove(info.InstanceId, out var removed))
            {
                InstanceMap.TryRemove(info.HostName, out removed);
            }
        }
    }
}
