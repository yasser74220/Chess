using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Baselib;
using Unity.Baselib.LowLevel;
using ErrorState = Unity.Baselib.LowLevel.Binding.Baselib_ErrorState;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// NetworkFamily indicates what type of underlying medium we are using.
    /// </summary>
    public enum NetworkFamily
    {
        Invalid = 0,
        Ipv4 = 2,
        Ipv6 = 23
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NetworkEndPoint
    {
        enum AddressType { Any = 0, Loopback = 1 }
        private const int rawIpv4Length = 4;
        private const int rawIpv6Length = 16;
        private const int rawDataLength = 16;               // Maximum space needed to hold a IPv6 Address
        private const int rawLength = rawDataLength + 3;    // SizeOf<Baselib_NetworkAddress>
        private static readonly bool IsLittleEndian = true;

        internal Binding.Baselib_NetworkAddress rawNetworkAddress;
        public int length;

        static NetworkEndPoint()
        {
            uint test = 1;
            byte* test_b = (byte*) &test;
            IsLittleEndian = test_b[0] == 1;
        }

        public ushort Port
        {
            get => (ushort) (rawNetworkAddress.port1 | (rawNetworkAddress.port0 << 8));
            set
            {
                rawNetworkAddress.port0 = (byte)((value >> 8) & 0xff);
                rawNetworkAddress.port1 = (byte)(value & 0xff);
            }
        }

        public NetworkFamily Family
        {
            get => FromBaselibFamily((Binding.Baselib_NetworkAddress_Family)rawNetworkAddress.family);
            set => rawNetworkAddress.family = (byte)ToBaselibFamily(value);
        }

        public NativeArray<byte> GetRawAddressBytes()
        {
            if (Family == NetworkFamily.Ipv4)
            {
                var bytes = new NativeArray<byte>(rawIpv4Length, Allocator.Temp);
                UnsafeUtility.MemCpy(bytes.GetUnsafePtr(), UnsafeUtility.AddressOf(ref rawNetworkAddress), rawIpv4Length);
                return bytes;
            }
            else if (Family == NetworkFamily.Ipv6)
            {
                var bytes = new NativeArray<byte>(rawIpv6Length, Allocator.Temp);
                UnsafeUtility.MemCpy(bytes.GetUnsafePtr(), UnsafeUtility.AddressOf(ref rawNetworkAddress), rawIpv6Length);
                return bytes;
            }
            return default;
        }

        public void SetRawAddressBytes(NativeArray<byte> bytes, NetworkFamily family = NetworkFamily.Ipv4)
        {
            if (family == NetworkFamily.Ipv4)
            {
                if (bytes.Length != rawIpv4Length)
                    throw new InvalidOperationException($"Bad input length, a ipv4 address is 4 bytes long not {bytes.Length}");

                UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref rawNetworkAddress), bytes.GetUnsafeReadOnlyPtr(), rawIpv4Length);
                Family = family;
            }
            else if (family == NetworkFamily.Ipv6)
            {
                if (bytes.Length != rawIpv6Length)
                    throw new InvalidOperationException($"Bad input length, a ipv6 address is 16 bytes long not {bytes.Length}");

                UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref rawNetworkAddress), bytes.GetUnsafeReadOnlyPtr(), rawIpv6Length);
                Family = family;
            }
        }

        public ushort RawPort
        {
            get
            {
                ushort *port = (ushort*)((byte*) UnsafeUtility.AddressOf(ref rawNetworkAddress) + rawDataLength);
                return *port;
            }
            set
            {
                ushort *port = (ushort*)((byte*) UnsafeUtility.AddressOf(ref rawNetworkAddress) + rawDataLength);
                *port = value;
            }
        }

        public string Address => AddressAsString();

        public bool IsValid => Family != 0;

        public static NetworkEndPoint AnyIpv4 => CreateAddress(0);
        public static NetworkEndPoint LoopbackIpv4 => CreateAddress(0, AddressType.Loopback);

        public static NetworkEndPoint AnyIpv6 => CreateAddress(0, AddressType.Any, NetworkFamily.Ipv6);
        public static NetworkEndPoint LoopbackIpv6 => CreateAddress(0, AddressType.Loopback, NetworkFamily.Ipv6);

        public NetworkEndPoint WithPort(ushort port)
        {
            var ep = this;
            ep.Port = port;
            return ep;
        }
        public bool IsLoopback => (this == LoopbackIpv4.WithPort(Port)) || (this == LoopbackIpv6.WithPort(Port));
        public bool IsAny => (this == AnyIpv4.WithPort(Port)) || (this == AnyIpv6.WithPort(Port));

        // Returns true if we can fully parse the input and return a valid endpoint
        public static bool TryParse(string address, ushort port, out NetworkEndPoint endpoint, NetworkFamily family = NetworkFamily.Ipv4)
        {
            UnsafeUtility.SizeOf<Binding.Baselib_NetworkAddress>();
            endpoint = default(NetworkEndPoint);

            var errorState = default(ErrorState);
            var ipBytes = System.Text.Encoding.UTF8.GetBytes(address + char.MinValue);

            fixed (byte* ipBytesPtr = ipBytes)
            fixed (Binding.Baselib_NetworkAddress* rawAddress = &endpoint.rawNetworkAddress)
            {
                Binding.Baselib_NetworkAddress_Encode(
                    rawAddress,
                    ToBaselibFamily(family),
                    ipBytesPtr,
                    (ushort) port,
                    &errorState);
            }

            if (errorState.code != Binding.Baselib_ErrorCode.Success)
            {
                return false;
            }
            return endpoint.IsValid;
        }
        // Returns a default address if parsing fails
        public static NetworkEndPoint Parse(string address, ushort port, NetworkFamily family = NetworkFamily.Ipv4)
        {
            if (TryParse(address, port, out var endpoint, family))
                return endpoint;

            return default;
        }

        public static bool operator ==(NetworkEndPoint lhs, NetworkEndPoint rhs)
        {
            return lhs.Compare(rhs);
        }

        public static bool operator !=(NetworkEndPoint lhs, NetworkEndPoint rhs)
        {
            return !lhs.Compare(rhs);
        }

        public override bool Equals(object other)
        {
            return this == (NetworkEndPoint) other;
        }

        public override int GetHashCode()
        {
            var p = (byte*) UnsafeUtility.AddressOf(ref rawNetworkAddress);
            unchecked
            {
                var result = 0;

                for (int i = 0; i < rawLength; i++)
                {
                    result = (result * 31) ^ (int)p[i];
                }

                return result;
            }
        }

        bool Compare(NetworkEndPoint other)
        {
            var p = (byte*) UnsafeUtility.AddressOf(ref rawNetworkAddress);
            var p1 = (byte*) UnsafeUtility.AddressOf(ref other.rawNetworkAddress);
            return UnsafeUtility.MemCmp(p, p1, rawLength) == 0;
        }

        private string AddressAsString()
        {
            switch (Family)
            {
                case NetworkFamily.Ipv4:
                    return string.Concat(
                        // TODO(steve): Update to use ipv4_0 ... 3 when its available.
                        rawNetworkAddress.data0, ".",
                        rawNetworkAddress.data1, ".",
                        rawNetworkAddress.data2, ".",
                        rawNetworkAddress.data3,
                        ":", Port
                    );
                case NetworkFamily.Ipv6:
                    const string numberFormat = "[{0:x}:{1:x}:{2:x}:{3:x}:{4:x}:{5:x}:{6:x}:{7:x}]:{8}";
                    // TODO(steve): Include scope and handle leading zeros
                    // TODO(steve): Update to use ipv6_0 ... 15 when its available.
                    return String.Format(numberFormat,
                        rawNetworkAddress.data1 | (rawNetworkAddress.data0 << 8),
                        rawNetworkAddress.data3 | (rawNetworkAddress.data2 << 8),
                        rawNetworkAddress.data5 | (rawNetworkAddress.data4 << 8),
                        rawNetworkAddress.data7 | (rawNetworkAddress.data6 << 8),
                        rawNetworkAddress.data9 | (rawNetworkAddress.data8 << 8),
                        rawNetworkAddress.data11 | (rawNetworkAddress.data10 << 8),
                        rawNetworkAddress.data13 | (rawNetworkAddress.data12 << 8),
                        rawNetworkAddress.data15 | (rawNetworkAddress.data14 << 8),
                        Port
                    );
                default:
                    // TODO(steve): Throw an exception?
                    return string.Empty;
            }
        }

        private static ushort ByteSwap(ushort val)
        {
            return (ushort) (((val & 0xff) << 8) | (val >> 8));
        }

        private static uint ByteSwap(uint val)
        {
            return (uint) (((val & 0xff) << 24) | ((val & 0xff00) << 8) | ((val >> 8) & 0xff00) | (val >> 24));
        }

        static NetworkEndPoint CreateAddress(ushort port, AddressType type = AddressType.Any, NetworkFamily family = NetworkFamily.Ipv4)
        {
            if (family == NetworkFamily.Invalid)
                return default;

            uint ipv4Loopback = (127 << 24) | 1;

            if (IsLittleEndian)
            {
                port = ByteSwap(port);
                ipv4Loopback = ByteSwap(ipv4Loopback);
            }

            var ep = new NetworkEndPoint
            {
                Family = family,
                RawPort = port,
                length = rawLength
            };

            if (type == AddressType.Loopback)
            {
                if (family == NetworkFamily.Ipv4)
                {
                    *(uint*) UnsafeUtility.AddressOf(ref ep.rawNetworkAddress) = ipv4Loopback;
                }
                else if (family == NetworkFamily.Ipv6)
                {
                    ep.rawNetworkAddress.data15 = 1;
                }
            }
            return ep;
        }

        static NetworkFamily FromBaselibFamily(Binding.Baselib_NetworkAddress_Family family)
        {
            if (family == Binding.Baselib_NetworkAddress_Family.IPv4)
                return NetworkFamily.Ipv4;
            if (family == Binding.Baselib_NetworkAddress_Family.IPv6)
                return NetworkFamily.Ipv6;
            return NetworkFamily.Invalid;
        }
        static Binding.Baselib_NetworkAddress_Family ToBaselibFamily(NetworkFamily family)
        {
            if (family == NetworkFamily.Ipv4)
                return Binding.Baselib_NetworkAddress_Family.IPv4;
            if (family == NetworkFamily.Ipv6)
                return Binding.Baselib_NetworkAddress_Family.IPv6;
            return Binding.Baselib_NetworkAddress_Family.Invalid;
        }
    }

    public unsafe struct NetworkInterfaceEndPoint
    {
        public int dataLength;
        public fixed byte data[56];

        public bool IsValid => dataLength != 0;

        public static bool operator ==(NetworkInterfaceEndPoint lhs, NetworkInterfaceEndPoint rhs)
        {
            return lhs.Compare(rhs);
        }

        public static bool operator !=(NetworkInterfaceEndPoint lhs, NetworkInterfaceEndPoint rhs)
        {
            return !lhs.Compare(rhs);
        }

        public override bool Equals(object other)
        {
            return this == (NetworkInterfaceEndPoint) other;
        }

        public override int GetHashCode()
        {
            fixed (byte* p = data)
                unchecked
                {
                    var result = 0;

                    for (int i = 0; i < dataLength; i++)
                    {
                        result = (result * 31) ^ (int)p[i];
                    }

                    return result;
                }
        }

        bool Compare(NetworkInterfaceEndPoint other)
        {
            // baselib doesn't return consistent lengths under posix, so lengths can
            // only be used as a shortcut if only one addresses a blank.
            if (dataLength != other.dataLength && (dataLength <= 0 || other.dataLength <= 0))
                return false;

            fixed (void* p = this.data)
            {
                return UnsafeUtility.MemCmp(p, other.data, math.min(dataLength, other.dataLength)) == 0;
            }
        }
    }
}