﻿using System;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Lib.AspNetCore.ServerSentEvents.Internals;

namespace Lib.AspNetCore.ServerSentEvents
{
    /// <summary>
    /// Service which provides operations over Server-Sent Events protocol.
    /// </summary>
    public class ServerSentEventsService : IServerSentEventsService
    {
        #region Fields
        private readonly ConcurrentDictionary<Guid, ServerSentEventsClient> _clients = new ConcurrentDictionary<Guid, ServerSentEventsClient>();
        #endregion

        #region Properties
        /// <summary>
        /// Gets the interval after which clients will attempt to reestablish failed connections.
        /// </summary>
        public uint? ReconnectInterval { get; private set; }
        #endregion

        #region Methods
        /// <summary>
        /// Gets the client based on the unique client identifier.
        /// </summary>
        /// <param name="clientId">The unique client identifier.</param>
        /// <returns>The client.</returns>
        public IServerSentEventsClient GetClient(Guid clientId)
        {
            ServerSentEventsClient client;

            _clients.TryGetValue(clientId, out client);

            return client;
        }

        /// <summary>
        /// Gets all clients.
        /// </summary>
        /// <returns>The clients.</returns>
        public IReadOnlyCollection<IServerSentEventsClient> GetClients()
        {
            return _clients.Values.ToArray();
        }

        /// <summary>
        /// Changes the interval after which clients will attempt to reestablish failed connections.
        /// </summary>
        /// <param name="reconnectInterval">The reconnect interval.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task ChangeReconnectIntervalAsync(uint reconnectInterval)
        {
            ReconnectInterval = reconnectInterval;

            byte[] reconnectIntervalBytes = Encoding.UTF8.GetBytes(reconnectInterval.ToString(CultureInfo.InvariantCulture));

            return ForAllClientsAsync(client => client.ChangeReconnectIntervalAsync(reconnectIntervalBytes));
        }

        /// <summary>
        /// Sends event to all clients.
        /// </summary>
        /// <param name="text">The simple text event.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task SendEventAsync(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);

            return ForAllClientsAsync(client => client.SendEventAsync(data));
        }

        /// <summary>
        /// Sends event to all clients.
        /// </summary>
        /// <param name="serverSentEvent">The event.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task SendEventAsync(ServerSentEvent serverSentEvent)
        {
            return ForAllClientsAsync(client => client.SendEventAsync(serverSentEvent));
        }

        /// <summary>
        /// When overriden in delivered class allows for recovery when client has reestablished the connection.
        /// </summary>
        /// <param name="client">The client who has reestablished the connection.</param>
        /// <param name="lastEventId">The identifier of last event which client has received.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public virtual Task OnReconnectAsync(IServerSentEventsClient client, string lastEventId)
        {
            return TaskHelper.GetCompletedTask();
        }
 
        internal void AddClient(ServerSentEventsClient client)
        {
            _clients.TryAdd(client.Id, client);
        }

        internal void RemoveClient(ServerSentEventsClient client)
        {
            client.IsConnected = false;

            _clients.TryRemove(client.Id, out client);
        }

        private Task ForAllClientsAsync(Func<ServerSentEventsClient, Task> clientOperationAsync)
        {
            List<Task> clientsTasks = new List<Task>();
            foreach (ServerSentEventsClient client in _clients.Values)
            {
                if (client.IsConnected)
                {
                    clientsTasks.Add(clientOperationAsync(client));
                }
            }

            return Task.WhenAll(clientsTasks);
        }
        #endregion
    }
}
