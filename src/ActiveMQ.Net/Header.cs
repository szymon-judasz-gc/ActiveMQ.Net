﻿namespace ActiveMQ.Net
{
    internal sealed class Header
    {
        private readonly Amqp.Framing.Header _innerHeader;

        public Header(Amqp.Message innerMessage)
        {
            _innerHeader = innerMessage.Header ??= new Amqp.Framing.Header();
        }

        public byte? Priority
        {
            get => _innerHeader.HasField(1) ? _innerHeader.Priority : default(byte?);
            set
            {
                if (value != default)
                    _innerHeader.Priority = value.Value;
                else
                    _innerHeader.ResetField(1);
            }
        }
    }
}