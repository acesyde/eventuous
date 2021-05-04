using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using JetBrains.Annotations;
using static Google.Cloud.PubSub.V1.PublisherClient;

// ReSharper disable InvertIf

namespace Eventuous.Producers.GooglePubSub {
    /// <summary>
    /// Producer for Google PubSub
    /// </summary>
    [PublicAPI]
    public class GooglePubSubProducer : BaseProducer<PubSubProduceOptions> {
        readonly ClientCreationSettings? _clientCreationSettings;
        readonly Settings?               _settings;
        readonly IEventSerializer        _serializer;
        PublisherClient?                 _client;
        readonly TopicName               _topicName;

        /// <summary>
        /// Create a new instance of a Google PubSub producer
        /// </summary>
        /// <param name="projectId">GCP project ID</param>
        /// <param name="topicId">Google PubSup topic ID (within the project). The topic must be created upfront.</param>
        /// <param name="serializer">Event serializer instance</param>
        /// <param name="clientCreationSettings">Optional: <see cref="ClientCreationSettings"/> for the <seealso cref="PublisherClient"/> creation</param>
        /// <param name="settings">Optional: <see cref="Settings"/> of the <seealso cref="PublisherClient"/></param>
        public GooglePubSubProducer(
            string                  projectId,
            string                  topicId,
            IEventSerializer        serializer,
            ClientCreationSettings? clientCreationSettings = null,
            Settings?               settings               = null
        ) {
            _clientCreationSettings = clientCreationSettings;
            _settings               = settings;
            _serializer             = Ensure.NotNull(serializer, nameof(serializer));

            _topicName = TopicName.FromProjectTopic(
                Ensure.NotEmptyString(projectId, nameof(projectId)),
                Ensure.NotEmptyString(topicId, nameof(topicId))
            );
        }

        public override async Task Initialize(CancellationToken cancellationToken = default) {
            _client = await CreateAsync(_topicName, _clientCreationSettings, _settings);
        }

        public override Task Shutdown(CancellationToken cancellationToken = default)
            => _client == null ? Task.CompletedTask : _client.ShutdownAsync(cancellationToken);

        protected override Task ProduceOne(
            object                message,
            Type                  type,
            PubSubProduceOptions? options,
            CancellationToken     cancellationToken
        ) {
            if (_client == null)
                throw new InvalidOperationException("Producer hasn't been initialized, call Initialize");
            
            var pubSubMessage = CreateMessage(message, type, options);

            return _client.PublishAsync(pubSubMessage);
        }

        protected override Task ProduceMany(
            IEnumerable<object>   messages,
            PubSubProduceOptions? options,
            CancellationToken     cancellationToken
        )
            => Task.WhenAll(messages.Select(x => ProduceOne(x, x.GetType(), options, cancellationToken)));

        PubsubMessage CreateMessage(object message, Type type, PubSubProduceOptions? options) {
            var psm = new PubsubMessage {
                Data        = ByteString.CopyFrom(_serializer.Serialize(message)),
                OrderingKey = options?.OrderingKey ?? "",
                Attributes = {
                    { "contentType", _serializer.ContentType },
                    { "eventType", TypeMap.GetTypeNameByType(type) }
                }
            };

            var attrs = options?.AddAttributes?.Invoke(message);

            if (attrs != null) {
                foreach (var (key, value) in attrs) {
                    psm.Attributes.Add(key, value);
                }
            }

            return psm;
        }
    }
}