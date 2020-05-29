using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;

namespace Ksis
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Enter hostname:");
            string s = Console.ReadLine();
            Program.MyTracert(s);
            Console.ReadKey();
        }

        //Константы
        public static int HeaderSize = 4;
        public static int MsgHeaderSize = 4;
        public static int IdentifierSize = 2;
        public static int SeqNumberSize = 2;
        public static int TypeOffset = 0;
        public static int CodeOffset = 1;
        public static int ChecksumOffset = 2;
        public static int IdentifierOffset = 4;
        public static int SeqNumberOffset = 6;
        public static int MessageOffset = 8;

        //Класс пакета
        class ICMP
        {
            public byte Type;
            public byte Code;
            public UInt16 Checksum;
            public int MessageSize;
            public UInt16 Identifier;
            public UInt16 SeqNumber;
            public byte[] Message;
            public ICMP()
            {
            }

            public ICMP(byte[] data, int size)
            {
                Type = data[20];
                Code = data[21];
                Checksum = BitConverter.ToUInt16(data, 22);
                MessageSize = size - 24;
                Message = data;
            }

            public byte[] getBytes()
            {
                byte[] data = new byte[MessageSize + HeaderSize];
                Buffer.BlockCopy(BitConverter.GetBytes(Type), 0, data, TypeOffset, 1);
                Buffer.BlockCopy(BitConverter.GetBytes(Code), 0, data, CodeOffset, 1);
                Buffer.BlockCopy(BitConverter.GetBytes(Checksum), 0, data, ChecksumOffset, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(Identifier), 0, data, IdentifierOffset, IdentifierSize);
                Buffer.BlockCopy(BitConverter.GetBytes(SeqNumber), 0, data, SeqNumberOffset, SeqNumberSize);
                Buffer.BlockCopy(Message, 0, data, MessageOffset, MessageSize - (IdentifierSize + SeqNumberSize));
                return data;
            }

            public UInt16 getChecksum()
            {
                UInt32 chcksm = 0;
                byte[] data = getBytes();
                int packetsize = MessageSize + HeaderSize;
                int index = 0;

                while (index < packetsize)
                {
                    chcksm += Convert.ToUInt32(BitConverter.ToUInt16(data, index));
                    index += 2;
                }
                return (UInt16)(~chcksm);
            }
        }

        //Перевод из строки в массив байт
        static byte[] ToByteArr(string str)
        {
            byte[] data = Encoding.ASCII.GetBytes(str);
            return data;
        }

        //Функция отправки, приёма и обработки пакетов с определённым ttl
        static Boolean SendRecieve(int i, Socket host, ICMP packet, EndPoint ep, IPEndPoint iep, int packetsize)
        {
            int badcount = 0;   //счётчик неудавшихся попыток
            byte[] data;
            int pcklength = 0;
            host.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, i);

            try
            {
                DateTime timestart = DateTime.Now;
                host.SendTo(packet.getBytes(), packetsize, SocketFlags.None, iep);
                data = new byte[1024];
                pcklength = host.ReceiveFrom(data, ref ep);
                TimeSpan timestop1 = DateTime.Now - timestart;
                ICMP response1 = new ICMP(data, pcklength);

                timestart = DateTime.Now;
                host.SendTo(packet.getBytes(), packetsize, SocketFlags.None, iep);
                data = new byte[1024];
                pcklength = host.ReceiveFrom(data, ref ep);
                TimeSpan timestop2 = DateTime.Now - timestart;
                ICMP response2 = new ICMP(data, pcklength);

                timestart = DateTime.Now;
                host.SendTo(packet.getBytes(), packetsize, SocketFlags.None, iep);
                data = new byte[1024];
                pcklength = host.ReceiveFrom(data, ref ep);
                TimeSpan timestop3 = DateTime.Now - timestart;
                ICMP response3 = new ICMP(data, pcklength);

                if (response1.Type == 11 || response2.Type == 11 || response3.Type == 11)
                {
                    Console.WriteLine(i + ": " + ep.ToString() + "     " + (timestop1.Milliseconds.ToString()) + " " + (timestop2.Milliseconds.ToString()) + " " + (timestop3.Milliseconds.ToString()) + " мс");
                    return false;
                }

                if (response1.Type == 0 || response2.Type == 0 || response3.Type == 0)
                {
                    Console.WriteLine(i + ": " + ep.ToString() + "     достигнут за " + i + " прыжков, " + (timestop1.Milliseconds.ToString()) + " " + (timestop2.Milliseconds.ToString()) + " " + (timestop3.Milliseconds.ToString()) + " мс");
                    return true;
                }

                return true;
            }
            catch (SocketException)
            {
                Console.WriteLine(i + ": нет ответа от " + ep + " ( dest: " + iep + ")");
                badcount++;

                if (badcount == 5)
                {
                    Console.WriteLine("Не удалось установить соединение\n");
                    return true;
                }
                return false;
            }
            finally
            {
                try
                {
                    string ip = ep.ToString();
                    ip = ip.Remove(ip.Length - 2, 2);
                    if (!ip.StartsWith("192.168"))
                    {
                        IPAddress addr = IPAddress.Parse(ip);
                        IPHostEntry entry = Dns.GetHostEntry(addr);
                        Console.WriteLine(entry.HostName);
                    }
                    Console.WriteLine();
                }
                catch (SocketException)
                {
                    Console.WriteLine("Нет DNS ответа\n");
                }
            }
        }

        //Основная программа
        static void MyTracert(String remoteHost)
        {
            //начальная инициализация
            byte[] data = new byte[1024];
            Socket host = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
            IPHostEntry iphe = Dns.Resolve(remoteHost);
            IPEndPoint iep = new IPEndPoint(iphe.AddressList[0], 0);
            EndPoint ep = (EndPoint)iep;
            ICMP packet = new ICMP();

            //формирование пакета
            packet.Type = 0x08;
            packet.Code = 0x00;
            packet.Checksum = 0;
            packet.Identifier = 1;
            packet.SeqNumber = 1;
            packet.Message = ToByteArr("test");
            packet.MessageSize = packet.Message.Length + MsgHeaderSize;
            int packetsize = packet.MessageSize + HeaderSize;

            UInt16 chcksum = packet.getChecksum();
            packet.Checksum = chcksum;

            host.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 3000);

            //цикл переборки ttl и обработки ответов
            for (int i = 1; i < 256; i++)
            {
                if (SendRecieve(i, host, packet, ep, iep, packetsize))
                {
                    break;
                }
            }
            host.Close();
        }
    }
}