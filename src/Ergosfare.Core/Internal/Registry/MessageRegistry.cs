using System.Collections.Concurrent;
using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Abstractions;
using Ergosfare.Core.Internal.Builders;
using Ergosfare.Core.Internal.Registry.Descriptors;


namespace Ergosfare.Core.Internal.Registry;

using System;
using System.Collections;
using System.Collections.Generic;

internal sealed class MessageRegistry : IMessageRegistry
{
    
    private readonly List<IHandlerDescriptorBuilder> _descriptorBuilders =
    [
        new HandlerDescriptorBuilder(),
    ];


    private readonly List<IHandlerDescriptor> _descriptors = [];
    private readonly List<MessageDescriptor> _messages = [];
    private readonly List<MessageDescriptor> _newMessages = [];
    private readonly ConcurrentDictionary<Type, byte> _processedTypes = new();
    public IEnumerator<IMessageDescriptor> GetEnumerator() => _messages.GetEnumerator();

    public int Count => _messages.Count;
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Register(Type type)
    {
        if (_processedTypes.ContainsKey(type))
        {
            return;
        }

        var newDescriptors = _descriptorBuilders
            .Where(d => d.CanBuild(type))
            .Select(d => d.Build(type))
            .ToList();

        if (newDescriptors.Count == 0)
        {
            RegisterMessage(type);
        }
        else
        {
            foreach (var descriptor in newDescriptors)
            {
                RegisterMessage(descriptor.MessageType);
                _descriptors.Add(descriptor);
            }

            if (newDescriptors.Count > 0 && _messages.Count > 0)
            {
                foreach (var messageDescriptor in _messages)
                {
                    messageDescriptor.AddDescriptors(newDescriptors);
                }
            }
        }

        // IMPORTANT: Promote newMessages to messages here
        if (_newMessages.Count > 0)
        {
            _messages.AddRange(_newMessages);
            _newMessages.Clear();
        }
    }
    private void RegisterMessage(Type messageType)
    {
        if (messageType.Namespace is not null && messageType.Namespace.StartsWith("System") )
        {
            return;
        }

        if (!messageType.IsAssignableTo(typeof(IMessage)))
        {
            throw new InvalidMessageTypeException(messageType);
        }

        messageType = messageType.IsGenericType ? messageType.GetGenericTypeDefinition() : messageType;

        // Check if this message type is already registered
        var existingMessage = _messages.FirstOrDefault(d => d.MessageType == messageType);

        if (existingMessage != null)
        {
            return;
        }

        var isNewMessage = _newMessages.FirstOrDefault(d => d.MessageType == messageType);

        if (isNewMessage != null)
        {
            return;
        }

        _newMessages.Add(new MessageDescriptor(messageType));
    }
}
