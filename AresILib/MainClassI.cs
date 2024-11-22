using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace AresILib;

public class MainClassI
{
	private static TcpClient? client;
	private static NetworkStream? netStream;
	private static byte[] toSend = [];
	private static Thread? thread;
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
		var receiveLen = GC.AllocateUninitializedArray<byte>(4);
		byte[] receiveMessage;
		while (true)
		{
			try
			{
				netStream?.Read(receiveLen, 0, 4);//чтение сообщения
				receiveMessage = GC.AllocateUninitializedArray<byte>(BitConverter.ToInt32(receiveLen));
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
					0 => () => MainThread(filename, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + "/" + Path.GetFileNameWithoutExtension(filename), Decompress),
					1 => () => MainThread(filename, Path.ChangeExtension(filename, ".ares-i"), Compress),
					2 => () => MainThread(filename, Path.GetDirectoryName(filename) + "/" + Path.GetFileNameWithoutExtension(filename), Decompress),
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

	public static void MainThread(string filename, string filename2, Action<string, string> action, bool send = true)
	{
		var tempFilename = "";
		try
		{
			Supertotal = 0;
			isWorking = true;
			//if (action == Compress)
			//	fragment_count = (int)Max(Min((new FileInfo(filename).Length + fragmentLength - 1) / fragmentLength, int.MaxValue / 10), 1);
			tempFilename = (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + "/Ares-" + Environment.ProcessId + ".tmp";
			action(filename, tempFilename);
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

	public static void Compress(string rfile, string wfile)
	{
		using var image = Image.Load<Rgba32>(rfile);
		var bytes = File.ReadAllBytes(rfile).ToNList();
		var s = ExecutionsI.Encode(image, bytes);
		File.WriteAllBytes(wfile, s);
	}

	public static void Decompress(string rfile, string wfile)
	{
		var bytes = File.ReadAllBytes(rfile);
		using var image = DecodingI.Decode(bytes, out transparency);
		if (transparency)
			image.SaveAsTga(wfile, new() { BitsPerPixel = SixLabors.ImageSharp.Formats.Tga.TgaBitsPerPixel.Pixel32, Compression = SixLabors.ImageSharp.Formats.Tga.TgaCompression.None });
		else
			image.SaveAsBmp(wfile);
	}

	private static void Recompress(string rfile, string wfile)
	{
	}
}
