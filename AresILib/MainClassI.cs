using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static System.Math;

namespace AresILib;

public class MainClassI
{
	private static TcpClient? client;
	private static NetworkStream? netStream;
	private static byte[] toSend = [];
	private static Thread? thread;
	private static FileStream rfs = default!;
	private static FileStream wfs = default!;
	private static bool isWorking, transparency = false;
	private static readonly object lockObj = new();

	public static void Main(string[] args)
	{
#if !RELEASE
		args = ["11000"];
		Thread.Sleep(MillisecondsPerSecond * 5 / 2);
#else
		Thread.Sleep(MillisecondsPerSecond / 2);
#endif
		if (!(args.Length != 0 && int.TryParse(args[0], out var port) && port >= 1024 && port <= 65535))
			return;
		if (args.Length >= 2 && int.TryParse(args[1], out var mainProcessId))
		{
			var mainProcess = Process.GetProcessById(mainProcessId);
			mainProcess.EnableRaisingEvents = true;
			mainProcess.Exited += (_, _) => Environment.Exit(0);
		}
		Connect(port);
	}

	public static void Connect(int port)
	{
		IPEndPoint ipe = new(IPAddress.Loopback, port); //IP с номером порта
		client = new(); //подключение клиента
		try
		{
			client.Connect(ipe);
			netStream = client.GetStream();
			Thread receiveThread = new(ReceiveData) { IsBackground = true, Name = "Подключение-I" };//получение данных
			receiveThread.Start();//старт потока
		}
		catch
		{
			return;
		}
		while (true)
			SendMessage();
	}

	public static void SendMessage()
	{
		try
		{
			Thread.Sleep(MillisecondsPerSecond / 4);
			if (netStream == null)
				Disconnect();
			else if (toSend.Length != 0)
			{
				var toSendLen = BitConverter.GetBytes(toSend.Length);
				netStream.Write(toSendLen);
				netStream.Write(toSend);
				netStream.Flush(); //удаление данных из потока
				toSend = [];
			}
		}
		catch
		{
			Disconnect();
		}
	}

	public static void ReceiveData()
	{
		var receiveLen = new byte[4];
		byte[] receiveMessage;
		while (true)
		{
			try
			{
				netStream?.Read(receiveLen, 0, 4);//чтение сообщения
				receiveMessage = new byte[BitConverter.ToInt32(receiveLen)];
				netStream?.Read(receiveMessage, 0, receiveMessage.Length);
				WorkUpReceiveMessage(receiveMessage);
			}
			catch
			{
				Console.ReadLine();
				Disconnect();
			}
		}
	}

	public static void WorkUpReceiveMessage(byte[] message)
	{
		try
		{
			if (message[0] == 0)
				PresentMethodsI = (UsedMethodsI)BitConverter.ToInt32(message.AsSpan(1..));
			else if (message[0] <= 4)
			{
				var filename = Encoding.UTF8.GetString(message[1..]);
				thread = new((message[0] - 2) switch
				{
					0 => () => MainThread(filename, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\" + Path.GetFileNameWithoutExtension(filename), Decompress),
					1 => () => MainThread(filename, Path.ChangeExtension(filename, ".ares-i"), Compress),
					2 => () => MainThread(filename, Path.GetDirectoryName(filename) + @"\" + Path.GetFileNameWithoutExtension(filename), Decompress),
					3 => () => MainThread(filename, filename, Recompress),
					_ => () => { }
					,
				})
				{ IsBackground = true, Name = "Основной процесс" };
				thread.Start();
				Thread thread2 = new(TransferProgress) { IsBackground = true, Name = "Передача прогресса" };
				thread2.Start();
			}
		}
		catch
		{
			lock (lockObj)
			{
				isWorking = false;
				toSend = [2];
				SendMessage();
			}
		}
	}

	public static void MainThread(string filename, string filename2, Action<FileStream, FileStream> action, bool send = true)
	{
		var tempFilename = "";
		try
		{
			Supertotal = 0;
			isWorking = true;
			//if (action == Compress)
			//	fragment_count = (int)Max(Min((new FileInfo(filename).Length + fragmentLength - 1) / fragmentLength, int.MaxValue / 10), 1);
			rfs = File.OpenRead(filename);
			tempFilename = (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresI-" + Environment.ProcessId + ".tmp";
			wfs = File.OpenWrite(tempFilename);
			action(rfs, wfs);
			rfs.Close();
			wfs.Close();
			File.Move(tempFilename, filename2 + (action != Decompress ? "" : transparency ? ".tga" : ".bmp"), true);
			lock (lockObj)
			{
				isWorking = false;
				toSend = [1, (byte)(transparency ? 1 : 0)];
				if (send)
					SendMessage();
			}
		}
		catch (DecoderFallbackException)
		{
			lock (lockObj)
			{
				isWorking = false;
				toSend = [(byte)(action == Compress ? 3 : 2)];
				if (send)
					SendMessage();
				else
					throw;
			}
		}
		catch
		{
			lock (lockObj)
			{
				isWorking = false;
				toSend = [2];
				if (send)
					SendMessage();
				else
					throw;
			}
		}
		finally
		{
			if (File.Exists(tempFilename))
			{
				rfs.Close();
				wfs.Close();
				File.Delete(tempFilename);
			}
		}
	}

	private static void TransferProgress()
	{
		Thread.Sleep(MillisecondsPerSecond);
		while (isWorking)
		{
			List<byte> list =
			[
				0,
				.. BitConverter.GetBytes(Supertotal),
				.. BitConverter.GetBytes(SupertotalMaximum),
				.. BitConverter.GetBytes(Total),
				.. BitConverter.GetBytes(TotalMaximum),
			];
			for (var i = 0; i < ProgressBarGroups; i++)
			{
				list.AddRange(BitConverter.GetBytes(Subtotal[i]));
				list.AddRange(BitConverter.GetBytes(SubtotalMaximum[i]));
				list.AddRange(BitConverter.GetBytes(Current[i]));
				list.AddRange(BitConverter.GetBytes(CurrentMaximum[i]));
				list.AddRange(BitConverter.GetBytes(Status[i]));
				list.AddRange(BitConverter.GetBytes(StatusMaximum[i]));
			}
			lock (lockObj)
				toSend = [.. list];
			Thread.Sleep(MillisecondsPerSecond);
		}
	}

	public static void Disconnect()
	{
		client?.Close();//отключение клиента
		netStream?.Close();//отключение потока
		Environment.Exit(0); //завершение процесса
	}

	public static void Compress(FileStream rfs, FileStream wfs)
	{
		using var image = Image.Load<Rgba32>(rfs);
		var bytes = new byte[rfs.Length];
		rfs.Position = 0;
		rfs.Read(bytes);
		var s = ExecutionsI.Encode(image, bytes);
		wfs.Write(s);
		//if (continue_)
		//{
		//	Supertotal = 0;
		//	SupertotalMaximum = fragment_count * 10;
		//	rfs_position = 0;
		//	wfs_position = 0;
		//	int fragment_count2 = fragment_count;
		//	BitArray bits = new(0);
		//	int i;
		//	for (i = fibonacciSequence.Length - 1; i >= 0; i--)
		//	{
		//		if (fibonacciSequence[i] <= fragment_count2)
		//		{
		//			bits = new BitArray(i + 2);
		//			bits[i] = true;
		//			bits[i + 1] = true;
		//			fragment_count2 -= fibonacciSequence[i];
		//			break;
		//		}
		//	}
		//	for (i--; i >= 0;)
		//	{
		//		if (fibonacciSequence[i] <= fragment_count2)
		//		{
		//			bits[i] = true;
		//			fragment_count2 -= fibonacciSequence[i];
		//			i -= 2;
		//		}
		//		else
		//			i--;
		//	}
		//	bytes = new byte[(bits.Length + 7) / 8];
		//	bits.CopyTo(bytes, 0);
		//	wfs.Write(bytes, 0, bytes.Length);
		//}
		//rfs_position = rfs.Position;
		//wfs_position = wfs.Position;
		//if (fragment_count != 1)
		//	bytes = new byte[fragmentLength];
		//for (; fragment_count > 0; fragment_count--)
		//{
		//	if (fragment_count == 1)
		//	{
		//		int left_length = (int)(rfs.Length % fragmentLength);
		//		if (left_length != 0)
		//			bytes = new byte[left_length];
		//	}
		//	rfs.Read(bytes, 0, bytes.Length);
		//	byte[] s = Executions.Encode(bytes);
		//	if (fragment_count != 1)
		//		wfs.Write(new byte[] { (byte)(s.Length / ValuesIn2Bytes), (byte)(s.Length % ValuesIn2Bytes >> BitsPerByte), (byte)s.Length }, 0, 3);
		//	wfs.Write(s, 0, s.Length);
		//	rfs_position = rfs.Position;
		//	wfs_position = wfs.Position;
		//	Supertotal += ProgressBarStep;
		//	GC.Collect();
		//}
	}

	public static void Decompress(FileStream rfs, FileStream wfs)
	{
		var bytes = new byte[rfs.Length];
		rfs.Read(bytes);
		using var image = DecodingI.Decode(bytes, out transparency);
		if (transparency)
			image.SaveAsTga(wfs, new() { BitsPerPixel = SixLabors.ImageSharp.Formats.Tga.TgaBitsPerPixel.Pixel32, Compression = SixLabors.ImageSharp.Formats.Tga.TgaCompression.None });
		else
			image.SaveAsBmp(wfs);
		//if (continue_)
		//{
		//	rfs_position = 0;
		//	wfs_position = 0;
		//	fragment_count = 0;
		//	BitArray bits;
		//	bytes = new byte[1];
		//	bool one = false, success = false;
		//	int sequencePos = 0;
		//	while (1 == 1)
		//	{
		//		rfs.Read(bytes, 0, 1);
		//		bits = new BitArray(bytes);
		//		for (int i = 0; i < bits.Length; i++)
		//		{
		//			if ((bits[i] && one) || sequencePos == fibonacciSequence.Length)
		//			{
		//				success = true;
		//				break;
		//			}
		//			else
		//			{
		//				if (bits[i])
		//					fragment_count += fibonacciSequence[sequencePos];
		//				sequencePos++;
		//				one = bits[i];
		//			}
		//		}
		//		if (success)
		//			break;
		//	}
		//	SupertotalMaximum = fragment_count * 10;
		//}
		//rfs_position = rfs.Position;
		//wfs_position = wfs.Position;
		//for (; fragment_count > 0; fragment_count--)
		//{
		//	if (fragment_count == 1)
		//	{
		//		bytes = new byte[Min(rfs.Length - rfs.Position, 8000002)];
		//		rfs.Read(bytes, 0, bytes.Length);
		//	}
		//	else
		//	{
		//		bytes = new byte[3];
		//		rfs.Read(bytes, 0, 3);
		//		int fragment_length = Min((bytes[0] << (BitsPerByte << 1)) + (bytes[1] << BitsPerByte) + bytes[2], 8000002);
		//		bytes = new byte[fragment_length];
		//		rfs.Read(bytes, 0, bytes.Length);
		//	}
		//	byte[] s = Executions.Decode(bytes);
		//	wfs.Write(s, 0, s.Length);
		//	rfs_position = rfs.Position;
		//	wfs_position = wfs.Position;
		//	Supertotal += ProgressBarStep;
		//	GC.Collect();
		//}
	}

	private static void Recompress(FileStream rfs, FileStream wfs)
	{
	}
}
