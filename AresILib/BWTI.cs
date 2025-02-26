﻿
namespace AresILib;
internal class BWTI(int tn)
{
	private readonly NList<ShortIntervalList> result = [];

	public NList<ShortIntervalList> Encode(NList<ShortIntervalList> input, int delta, bool enoughTransparency)
	{
		if (input.Length == 0)
			throw new EncoderFallbackException();
		if (input[0].Contains(HuffmanApplied) || input[0].Contains(BWTApplied))
			return input;
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		var lz = CreateVar(input[0].IndexOf(LempelZivApplied), out var lzIndex) != -1;
		var startPos = (lz ? (input[0].Length >= lzIndex + BWTBlockExtraSize && input[0][lzIndex + 1] == LempelZivSubdivided ? 3 : 2) : 1) + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : 0) + 2;
		if (!(input.Length >= startPos + BWTBlockExtraSize && input.GetSlice(startPos).All(x => x.Length == 1 && x[0].Base == ValuesInByte)))
			throw new EncoderFallbackException();
		result.Replace(input.GetSlice(0, startPos));
		result[0] = new(result[0]);
		result[2] = new(result[2]) { new((uint)BitsCount((uint)BWTBlockSize) - 14, 18) };
		Status[tn] = 0;
		StatusMaximum[tn] = 7;
		var byteInput = input.GetSlice(startPos).ToNList(x => (byte)x[0].Lower);
		BitList bitInput = new(byteInput.Length);
		using BitList bitInputBetweenZLE = new(BWTBlockExtraSize, false);
		Status[tn]++;
		var uniqueElems = byteInput.ToHashSet();
		Status[tn]++;
		var uniqueElems2 = uniqueElems.ToNList().Sort();
		Status[tn]++;
		var inputPos = startPos;
		NList<byte> byteResult;
		Status[tn] = 0;
		StatusMaximum[tn] = byteInput.Length;
		Current[tn] += ProgressBarStep;
		byteResult = byteInput.Copy().AddRange(RedStarLinq.NEmptyList<byte>(GetArrayLength(byteInput.Length, BWTBlockSize) * BWTBlockExtraSize));
		BWTInternal();
		Status[tn] = 0;
		StatusMaximum[tn] = byteResult.Length;
		Current[tn] += ProgressBarStep;
		byteInput.Clear();
		for (var i = 0; i < byteResult.Length; i += BWTBlockSize)
		{
			byteInput.AddRange(byteResult.GetRange(i..(i += BWTBlockExtraSize)));
			using var zle = ZLE(byteResult.Skip(i).Take(BWTBlockSize), byteInput.GetRange(^BWTBlockExtraSize..), uniqueElems2[0], out var zerosB);
			byteInput.AddRange(zle);
			bitInput.AddRange(bitInputBetweenZLE).AddRange(zerosB);
		}
		uniqueElems2 = byteResult.Filter((x, index) => index % (BWTBlockSize + BWTBlockExtraSize) >= BWTBlockExtraSize).ToHashSet().ToNList().Sort();
		byteResult.Dispose();
		result.AddRange(byteInput.Combine(bitInput, (x, y) => y ? ByteIntervalsPlus1[ValuesInByte] : ByteIntervalsPlus1[x]));
		byteInput.Dispose();
		result[0].Add(BWTApplied);
		uniqueElems.ExceptWith(uniqueElems2);
#if DEBUG
		var input2 = input.Skip(startPos);
		var decoded = new BWTDec(result.GetRange(startPos), input[2][0].Lower, delta, enoughTransparency, BWTBlockSize).Decode([.. uniqueElems]);
		var decodedExpanded = decoded.ConvertAndJoin(l => (input[2][0].Lower == 0 ? l.GetSlice(1) : enoughTransparency && l.Length >= 4 && l[1].Base == 1 && l[2].Base == 1 && l[3].Base == 1 ? l.GetSlice(4).Prepend(l[0]) : l.GetSlice()).Convert(x => new ShortIntervalList([new(x.Lower, x.Length, ValuesInByte)]))).ToList();
		for (var i = 0; i < input2.Length && i < decodedExpanded.Length; i++)
			for (var j = 0; j < input2[i].Length && j < decodedExpanded[i].Length; j++)
			{
				var x = input2[i][j];
				var y = decodedExpanded[i][j];
				if (!(x.Equals(y) || GetBaseWithBuffer(x.Base) == y.Base && x.Lower == y.Lower && x.Length == y.Length))
					throw new DecoderFallbackException();
			}
		if (input2.Length != decodedExpanded.Length)
			throw new DecoderFallbackException();
#endif
		var c = uniqueElems.PConvert(x => new Interval(x, ValuesInByte));
		c.Insert(0, GetCountList((uint)uniqueElems.Length));
		var cSplit = c.SplitIntoEqual(8);
		c.Dispose();
		var cLength = (uint)cSplit.Length;
		result[0].Add(new(0, cLength, cLength));
		result[1].Set(^1, new(result[1][^1].Lower % 8 + 16, 24));
		result.Insert(startPos, cSplit.PConvert(x => new ShortIntervalList(x)));
		cSplit.Dispose();
		return result;
		void BWTInternal()
		{
			var buffer = RedStarLinq.FillArray(Environment.ProcessorCount, index => byteInput.Length < BWTBlockSize * (index + 1) ? default! : RedStarLinq.NEmptyList<byte>(BWTBlockSize * 2 - 1));
			var currentBlock = RedStarLinq.FillArray(buffer.Length, index => byteInput.Length < BWTBlockSize * (index + 1) ? default! : RedStarLinq.NEmptyList<byte>(BWTBlockSize));
			var indexes = RedStarLinq.FillArray(buffer.Length, index => byteInput.Length < BWTBlockSize * (index + 1) ? default! : RedStarLinq.NEmptyList<int>(BWTBlockSize));
			var tasks = new Task[buffer.Length];
			var MTFMemory = RedStarLinq.FillArray<byte[]>(buffer.Length, _ => default!);
			var multiThreadedCompare = buffer[buffer.Length * 3 / 4] == null;
			for (var i = 0; i < GetArrayLength(byteInput.Length, BWTBlockSize); i++)
			{
				tasks[i % buffer.Length]?.Wait();
				int i2 = i * BWTBlockSize, length = Min(BWTBlockSize, byteInput.Length - i2);
				MTFMemory[i % buffer.Length] = [.. uniqueElems2];
				if (byteInput.Length - i2 < BWTBlockSize)
				{
					buffer[i % buffer.Length] = default!;
					currentBlock[i % buffer.Length] = default!;
					indexes[i % buffer.Length] = default!;
					GC.Collect();
					buffer[i % buffer.Length]?.Dispose();
					currentBlock[i % buffer.Length]?.Dispose();
					indexes[i % buffer.Length]?.Dispose();
					buffer[i % buffer.Length] = RedStarLinq.NEmptyList<byte>((byteInput.Length - i2) * 2 - 1);
					currentBlock[i % buffer.Length] = RedStarLinq.NEmptyList<byte>(byteInput.Length - i2);
					indexes[i % buffer.Length] = RedStarLinq.NEmptyList<int>(byteInput.Length - i2);
				}
				for (var j = 0; j < length; j++)
					currentBlock[i % buffer.Length][j] = byteInput[i2 + j];
				var i3 = i;
				tasks[i % buffer.Length] = Task.Factory.StartNew(() => BWTMain(i3));
			}
			tasks.ForEach(x => x?.Wait());
			buffer.ForEach(x => x?.Dispose());
			currentBlock.ForEach(x => x?.Dispose());
			indexes.ForEach(x => x?.Dispose());
			void BWTMain(int blockIndex)
			{
				var firstPermutation = 0;
				//Сортировка контекстов с обнаружением, в какое место попал первый
				GetBWT(currentBlock[blockIndex % buffer.Length]!, buffer[blockIndex % buffer.Length]!, indexes[blockIndex % buffer.Length], currentBlock[blockIndex % buffer.Length]!, ref firstPermutation);
				for (var i = BWTBlockExtraSize - 1; i >= 0; i--)
				{
					byteResult[(BWTBlockSize + BWTBlockExtraSize) * blockIndex + i] = unchecked((byte)firstPermutation);
					firstPermutation >>= BitsPerByte;
				}
				WriteToMTF(blockIndex);
			}
			void GetBWT(NList<byte> source, NList<byte> buffer, NList<int> indexes, NList<byte> result, ref int firstPermutation)
			{
				buffer.SetRange(0, source);
				buffer.SetRange(source.Length, source[..^1]);
				for (var i = 0; i < indexes.Length; i++)
					indexes[i] = i;
				var indexesToSort = buffer.BWTCompare(source.Length, multiThreadedCompare ? tn : -1);
				indexesToSort.ForEach(i => indexes.Sort(x => buffer[i + x]));
				indexesToSort.Dispose();
#if DEBUG
				if (!indexes.AllUnique())
					throw new InvalidOperationException();
#endif
				firstPermutation = indexes.IndexOf(0);
				// Копирование результата
				for (var i = 0; i < source.Length; i++)
					result[i] = buffer[indexes[i] + indexes.Length - 1];
			}
			void WriteToMTF(int blockIndex)
			{
				for (var i = 0; i < currentBlock[blockIndex % buffer.Length].Length; i++)
				{
					var elem = currentBlock[blockIndex % buffer.Length][i];
					var index = Array.IndexOf(MTFMemory[blockIndex % buffer.Length]!, elem);
					byteResult[(BWTBlockSize + BWTBlockExtraSize) * blockIndex + i + BWTBlockExtraSize] = uniqueElems2[index];
					Array.Copy(MTFMemory[blockIndex % buffer.Length]!, 0, MTFMemory[blockIndex % buffer.Length]!, 1, index);
					MTFMemory[blockIndex % buffer.Length][0] = elem;
					Status[tn]++;
				}
			}
		}
	}

	private static Slice<byte> RLEAfterBWT(Slice<byte> input, NList<byte> firstPermutationRange, byte zero)
	{
		if ((firstPermutationRange[0] & ValuesInByte >> 1) != 0)
			return input;
		var result = new NList<byte>(input.Length + 1) { zero };
		for (var i = 0; i < input.Length;)
		{
			result.Add(input[i++]);
			if (i == input.Length)
				break;
			var j = i;
			while (i < input.Length && i - j < ValuesIn2Bytes && input[i] != zero)
				i++;
			if (i != j)
				result.AddRange(i - j < ValuesInByte >> 1 ? [(byte)(i - j - 1 + (ValuesInByte >> 1))] : [(ValuesInByte - 1), (byte)((i - j - (ValuesInByte >> 1)) >> BitsPerByte), (byte)(i - j - (ValuesInByte >> 1))]).AddRange(input.GetSlice(j..i));
			if (i - j >= ValuesIn2Bytes)
				continue;
			j = i;
			while (i < input.Length && i - j < ValuesIn2Bytes && input[i] == zero)
				i++;
			if (i != j)
				result.AddRange(i - j < ValuesInByte >> 1 ? [(byte)(i - j - 1)] : [((ValuesInByte >> 1) - 1), (byte)((i - j - (ValuesInByte >> 1)) >> BitsPerByte), (byte)(i - j - (ValuesInByte >> 1))]);
		}
#if DEBUG
		var input2 = input;
		var pos = 0;
		var decoded = new BWTDec(default!, default!, default!, default!, BWTBlockSize).DecodeRLEAfterBWT(result.GetSlice(), ref pos);
		for (var i = 0; i < input2.Length && i < decoded.Length; i++)
		{
			var x = input2[i];
			var y = decoded[i];
			if (x != y)
				throw new DecoderFallbackException();
		}
		if (input2.Length != decoded.Length)
			throw new DecoderFallbackException();
#endif
		if (result.Length < input.Length * 0.936)
		{
			firstPermutationRange[0] |= ValuesInByte >> 2;
			return result.GetSlice();
		}
		else
			return input;
	}

	private Slice<byte> ZLE(Slice<byte> input, NList<byte> firstPermutationRange, byte zero, out Slice<bool> zerosB)
	{
		var frequency = new int[ValuesInByte];
		for (var i = 0; i < input.Length; i++)
			frequency[input[i]]++;
		var zeroB = Array.IndexOf(frequency, 0);
		var preResult = new NList<short>(input.Length + 2) { zero, (short)zeroB };
		for (var i = 0; i < input.Length;)
		{
			while (i < input.Length && input[i] != zero)
			{
				preResult.Add(input[i++]);
				Status[tn]++;
			}
			if (i >= input.Length)
				break;
			var j = i;
			while (i < input.Length && input[i] == zero)
			{
				i++;
				Status[tn]++;
			}
			if (i == j)
				throw new EncoderFallbackException();
			using var toAdd = ((MpzT)(i - j + 1)).ToString(2)?.Skip(1).ToNList(x => (short)(x == '1' ? zeroB : x == '0' ? zero : throw new EncoderFallbackException()));
			if (toAdd != null)
				preResult.AddRange(toAdd);
		}
#if DEBUG
		var input2 = input;
		var pos = 0;
		var decoded = new BWTDec(default!, default!, default!, default!, BWTBlockSize).DecodeZLE(preResult.GetSlice(), ref pos);
		for (var i = 0; i < input2.Length && i < decoded.Length; i++)
		{
			var x = input2[i];
			var y = decoded[i];
			if (x != y)
				throw new DecoderFallbackException();
		}
		if (input2.Length != decoded.Length)
			throw new DecoderFallbackException();
		decoded.Dispose();
#endif
		if (preResult.Length < input.Length * 0.936)
		{
			firstPermutationRange[0] |= ValuesInByte >> 1;
			zerosB = preResult.Convert(x => x is < 0 or >= ValuesInByte);
			return preResult.Convert(x => unchecked((byte)x));
		}
		else
		{
			zerosB = new BitList(input.Length, false).GetSlice();
			return input;
		}
	}
}
