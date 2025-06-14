﻿#region Namespaces
#endregion

namespace AradSMPP.Net;

/// <summary> This command is used to transfer data between the SMSC (Short Message Service Centre) and the
/// ESME (Extended Short Message Entity). It may be used by both the ESME and SMSC  </summary>
public class DataSm : Header, IPacket, IPduDetails
{
    #region Public Properties

    /// <summary> Application service associated with the message </summary>
    public string? ServiceType { get; set; }

    /// <summary> Source address type of number </summary>
    public byte SourceTon { get; set; }

    /// <summary> Source address numbering plan indicator </summary>
    public byte SourceNpi { get; set; }

    /// <summary> Source phone number </summary>
    public string? SourceAddr { get; set; }

    /// <summary> Destination address type of number </summary>
    public byte DestTon { get; set; }

    /// <summary> Destination address numbering plan indicator </summary>
    public byte DestNpi { get; set; }

    /// <summary> Destination phone number </summary>
    public string? DestAddr { get; set; }

    /// <summary> Extended short message class </summary>
    public byte EsmClass { get; set; }

    /// <summary> Indicates if the message is a registered short message and thus if a Delivery Receipt is required upon the message attaining a final state </summary>
    public byte RegisteredDelivery { get; set; }

    /// <summary> Indicates the encoding scheme of the payload data </summary>
    public DataCodings DataCoding { get; set; }

    /// <summary> The smpp data packet </summary>
    private SmppBuffer UserDataBuffer { get; set; }

    /// <summary> The user data portion of the data packet </summary>
    private UserData UserData { get; set; }

    /// <summary> A reference assigned by the originating SME to the short message </summary>
    public ushort MessageReferenceNumber { get; set; }

    /// <summary> Total number of short messages within the concatenated short message </summary>
    public byte TotalSegments { get; set; }

    /// <summary> Sequence number of a particular short message within the concatenated short message </summary>
    public byte SequenceNumber { get; set; }

    /// <summary> Optional Parameters </summary>
    public TlvCollection Optional { get; set; }

    /// <summary> Indicates message mode associated with the short message </summary>
    public MessageModes MessageMode { get => (MessageModes) (EsmClass & 0x03);
        set => EsmClass |= (byte) value;
    }

    /// <summary> Indicates message type associated with the short message </summary>
    public MessageTypes MessageType { get => (MessageTypes) (EsmClass & 0x3c);
        set => EsmClass |= (byte) value;
    }

    /// <summary> Indicates GSM Network Specific Features associated with the short message </summary>
    public GsmSpecificFeatures MessageFeature => (GsmSpecificFeatures) (EsmClass & 0xc0);

    /// <summary> SMSC Delivery Receipt </summary>
    public SmscDeliveryReceipt SmscReceipt { get => (SmscDeliveryReceipt) (RegisteredDelivery & 0x03);
        set => RegisteredDelivery |= (byte) value;
    }

    /// <summary> SME originated Acknowledgement </summary>
    public SmeAcknowledgement Acknowledgement { get => (SmeAcknowledgement) (RegisteredDelivery & 0x0C);
        set => RegisteredDelivery |= (byte) value;
    }

    /// <summary> Intermediate Notification </summary>
    public IntermediateNotification Notification { get => (IntermediateNotification) (RegisteredDelivery & 0x10);
        set => RegisteredDelivery |= (byte) value;
    }

    #endregion

    #region Constructor

    /// <summary> Constructor </summary>
    /// <param name="defaultEncoding"></param>
    private DataSm(DataCodings defaultEncoding) : base(defaultEncoding, CommandSet.DataSm)
    {
        Optional = [];
    }

    #endregion

    #region Factory Methods

    /// <summary> Called to create a DataSm object </summary>
    /// <param name="defaultEncoding"></param>
    /// <param name="buf"></param>
    /// <param name="offset"></param>
    /// <returns> DataSm </returns>
    public static DataSm? Create(DataCodings defaultEncoding, SmppBuffer buf, ref int offset)
    {
        DataSm? dataSm = new(defaultEncoding);

        try
        {
            int startOffset = offset;

            buf.ExtractHeader(dataSm, ref offset);

            dataSm.ServiceType = buf.ExtractCString(ref offset);
            dataSm.SourceTon = buf.ExtractByte(ref offset);
            dataSm.SourceNpi = buf.ExtractByte(ref offset);
            dataSm.SourceAddr = buf.ExtractCString(ref offset);
            dataSm.DestTon = buf.ExtractByte(ref offset);
            dataSm.DestNpi = buf.ExtractByte(ref offset);
            dataSm.DestAddr = buf.ExtractCString(ref offset);
            dataSm.EsmClass = buf.ExtractByte(ref offset);
            dataSm.RegisteredDelivery = buf.ExtractByte(ref offset);
            dataSm.DataCoding = (DataCodings) buf.ExtractByte(ref offset);

            if (offset - startOffset < dataSm.Length)
            {
                if (dataSm.Optional == null)
                {
                    dataSm.Optional = [];
                }

                while (offset - startOffset < dataSm.Length)
                {
                    dataSm.Optional.Add(buf.ExtractTlv(ref offset));
                }
            }

            if (dataSm.Optional != null && dataSm.Optional.Count > 0)
            {
                Tlv tlvPayload = dataSm.Optional[OptionalTags.MessagePayload];
                if (tlvPayload != null)
                {
                    dataSm.UserDataBuffer = new(defaultEncoding, tlvPayload.Value);
                }

                Tlv tlvGlobalSeq = dataSm.Optional[OptionalTags.SarMsgRefNum];
                if (tlvGlobalSeq != null)
                {
                    dataSm.MessageReferenceNumber = SmppBuffer.BytesToShort(tlvGlobalSeq.Value, 0);
                }

                Tlv tlvPocketsNumber = dataSm.Optional[OptionalTags.SarTotalSegments];
                if (tlvPocketsNumber != null)
                {
                    dataSm.TotalSegments = tlvPocketsNumber.Value[0];
                }

                Tlv tlvLocalSeq = dataSm.Optional[OptionalTags.SarSegmentSeqnum];
                if (tlvLocalSeq != null)
                {
                    dataSm.SequenceNumber = tlvLocalSeq.Value[0];
                }
            }

            if (dataSm.UserDataBuffer != null && dataSm.UserDataBuffer.Length > 0)
            {
                dataSm.UserData = UserData.Create(dataSm.UserDataBuffer, dataSm.MessageFeature == GsmSpecificFeatures.Udhi);
            }
            else
            {
                dataSm.UserData = UserData.Create();
            }
        }

        catch
        {
            dataSm = null;
        }

        return dataSm;
    }

    #endregion

    #region PDU Detail Methods

    /// <summary> Called to return a list of property details from the PDU </summary>
    /// <returns> List PduPropertyDetail </returns>
    public List<PduPropertyDetail> Details()
    {
        List<PduPropertyDetail> details = null;

        try
        {
            int offset = 0;

            details = PduData.ExtractHeaderDetails(ref offset);

            details.Add(PduData.ExtractCString("ServiceType", ref offset));
            details.Add(PduData.ExtractByte("SourceTon", ref offset));
            details.Add(PduData.ExtractByte("SourceNpi", ref offset));
            details.Add(PduData.ExtractCString("SourceAddr", ref offset));
            details.Add(PduData.ExtractByte("DestTon", ref offset));
            details.Add(PduData.ExtractByte("DestNpi", ref offset));
            details.Add(PduData.ExtractCString("DestAddr", ref offset));

            PduPropertyDetail esmClass = PduData.ExtractByte("EsmClass", ref offset);
            details.Add(esmClass);

            details.Add(PduData.ExtractByte("EsmClass", ref offset));
            details.Add(PduData.ExtractByte("RegisteredDelivery", ref offset));
            details.Add(PduData.ExtractByte("DataCoding", ref offset));

            while (offset < PduData.Length)
            {
                PduData.ExtractTlv(details, ref offset);
     
                PduPropertyDetail tlvTag = details[details.Count - 3];
                PduPropertyDetail tlvLength = details[details.Count - 2];
                PduPropertyDetail tlvValue = details[details.Count - 1];

                switch (tlvTag.ValueUShort)
                {
                    case (ushort) OptionalTags.MessagePayload:
                        GsmSpecificFeatures messageFeature = (GsmSpecificFeatures) (esmClass.ValueByte & 0xc0);
                        SmppBuffer userData = new(DefaultEncoding, tlvValue.DataBlock);
                        userData.ExtractUserData(details, messageFeature == GsmSpecificFeatures.Udhi, offset);
                        break;
                        
                    case (ushort) OptionalTags.SarMsgRefNum:
                        tlvValue.PduDataType = PduDataTypes.UShort;
                        tlvValue.Name = "SARReferenceNumber";
                        tlvValue.ValueUShort = SmppBuffer.BytesToShort(tlvValue.DataBlock, 0);
                        break;

                    case (ushort) OptionalTags.SarTotalSegments:
                        tlvValue.PduDataType = PduDataTypes.Byte;
                        tlvValue.Name = "SARTotalSegments";
                        tlvValue.ValueByte = tlvValue.DataBlock[0];
                        break;

                    case (ushort) OptionalTags.SarSegmentSeqnum:
                        tlvValue.PduDataType = PduDataTypes.Byte;
                        tlvValue.Name = "SARSequenceNumber";
                        tlvValue.ValueByte = tlvValue.DataBlock[0];
                        break;
                }
            }
        }

        catch
        {
        }

        return details;
    }

    #endregion

    #region IPacket Methods

    /// <summary> Called to return the PDU for this type of object </summary>
    /// <returns> byte[] </returns>
    public byte[] GetPdu()
    {
        if (UserData.Headers.Count > 0)
        {
            EsmClass |= 0x40;
        }

        SmppBuffer tmpBuff = new(DefaultEncoding, this);

        tmpBuff.AddCString(ServiceType);
        tmpBuff.AddByte(SourceTon);
        tmpBuff.AddByte(SourceNpi);
        tmpBuff.AddCString(SourceAddr);
        tmpBuff.AddByte(DestTon);
        tmpBuff.AddByte(DestNpi);
        tmpBuff.AddCString(DestAddr);
        tmpBuff.AddByte(EsmClass);
        tmpBuff.AddByte(RegisteredDelivery);
        tmpBuff.AddByte((byte) DataCoding);
        tmpBuff.AddTlvCollection(Optional);

        tmpBuff.AddFinalLength();

        return tmpBuff.Buffer;
    }

    #endregion
}