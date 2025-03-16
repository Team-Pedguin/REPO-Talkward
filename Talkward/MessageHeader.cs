using System.Text;

namespace Talkward;

internal readonly struct MessageHeader
{
    public readonly int Voice;
    public readonly int SenderLength;
    public readonly int MessageLength;

    public MessageHeader(int voice, int senderLength, int messageLength)
    {
        Voice = voice;
        SenderLength = senderLength;
        MessageLength = messageLength;
    }

    public ReadOnlySpan<byte> Sender
    {
        get
        {
            ref var self = ref Unsafe.AsRef(this);
            ref var after = ref Unsafe.As<MessageHeader, byte>(ref Unsafe.Add(ref self, 1));
            return MemoryMarshal.CreateReadOnlySpan(ref after, SenderLength);
        }
    }

    public ReadOnlySpan<byte> Message
    {
        get
        {
            ref var self = ref Unsafe.AsRef(this);
            ref var after = ref Unsafe.As<MessageHeader, byte>(ref Unsafe.Add(ref self, 1));
            ref var message = ref Unsafe.Add(ref after, SenderLength);
            return MemoryMarshal.CreateReadOnlySpan(ref message, MessageLength);
        }
    }

    private MessageContent Parse()
    {
        var utf8 = Encoding.UTF8;
        var message = new MessageContent
        {
            Voice = Voice,
            Sender = utf8.GetString(Sender),
            Message = utf8.GetString(Message)
        };
        return message;
    }

    public static implicit operator MessageContent(MessageHeader header)
        => header.Parse();
}