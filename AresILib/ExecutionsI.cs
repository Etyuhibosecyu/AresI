using System.Diagnostics;

namespace AresILib;

public static partial class ExecutionsI
{
	private const int LZDictionarySize = 32767;
	private static ShortIntervalList widthAndHeightIntervals = [];

	private static List<ShortIntervalList> Alpha(this List<ShortIntervalList> input)
	{
		var input2 = input.GetSlice(2);
		var r = 0;
		foreach (var list in input2)
		{
			if (list[0].Lower == ValuesInByte - 1)
			{
			}
			else if (list[0].Lower == 0)
				r = Max(r, 1);
			else
				r = Max(r, 2);
		}
		foreach (var list in input2)
		{
			if (r == 0)
				list[0] = Interval.Default;
			else if (r == 1)
				list[0] = new(list[0].Lower / (ValuesInByte - 1), 2);
			if (r != 0 && list[0].Lower == 0)
			{
				list[1] = Interval.Default;
				list[2] = Interval.Default;
				list[3] = Interval.Default;
			}
		}
		input.Insert(2, new ShortIntervalList() { new((uint)r, 3) });
		return input;
	}

	private static List<ShortIntervalList> Delta(this List<ShortIntervalList> input)
	{
		if (input.Length < 5)
			return input;
		ShortIntervalList temp = input[3], temp2;
		for (var i = 4; i < input.Length; i++)
		{
			var list = input[i];
			temp2 = new(list);
			for (var j = 0; j < list.Length; j++)
				list[j] = new((list[j].Lower + list[j].Base * 3 / 2 - temp[j].Lower) % list[j].Base, list[j].Base);
			temp = temp2;
		}
		return input;
	}

	private static List<ShortIntervalList> UltraDelta(this List<ShortIntervalList> input)
	{
		if (input.Length < 6)
			return input;
		ShortIntervalList grandPrev = input[3], prev = input[4], current;
		for (var i = 5; i < input.Length; i++)
		{
			var list = input[i];
			current = new(list);
			for (var j = 0; j < list.Length; j++)
				list[j] = new((list[j].Lower + list[j].Base * 3 / 2 - prev[j].Lower * 2 + grandPrev[j].Lower) % list[j].Base, list[j].Base);
			grandPrev = prev;
			prev = current;
		}
		return input;
	}

	private static List<List<ShortIntervalList>> ToTripleList(this List<List<Rgba32>> input)
	{
		List<List<ShortIntervalList>> result = new(input.Length + 1) { new() { new() { PixelsApplied }, new(widthAndHeightIntervals) { new(0, 36), new(0, 24) } } };
		for (var i = 0; i < input.Length; i++)
		{
			result.Add(new(input[i].Length));
			for (var j = 0; j < input[i].Length; j++)
			{
				result[^1].Add([]);
				var color = input[i][j];
				result[^1][^1].Add(new(color.A, ValuesInByte));
				result[^1][^1].Add(new(color.R, ValuesInByte));
				result[^1][^1].Add(new(color.G, ValuesInByte));
				result[^1][^1].Add(new(color.B, ValuesInByte));
			}
		}
		return result;
	}

	public static ShortIntervalList GetWidthAndHeightIntervals<T>(List<List<T>> input)
	{
		ShortIntervalList result = [];
		result.WriteCount((uint)(input.Length - 1));
		result.WriteCount((uint)(input[0].Length - 1));
		return result;
	}

	public static List<List<ShortIntervalList>> Traverse(this List<List<ShortIntervalList>> input, TraversalMode mode)
	{
		if (mode == TraversalMode.Table)
			return input;
		else if (mode == TraversalMode.TableV)
			return input[1..].Transpose().Insert(0, input[0]);
		else if (mode == TraversalMode.Diagonal)
		{
			var input2 = input.GetSlice(1);
			List<List<ShortIntervalList>> newList = [];
			for (var i = 0; i < input2.Length; i++)
			{
				newList.Add([]);
				for (var j = 0; j < input2[0].Length && j <= i; j++)
					newList[^1].Add(input2[i - j][j]);
			}
			for (var i = 1; i < input2[0].Length; i++)
			{
				newList.Add([]);
				for (var j = 0; j < input2.Length && j < input2[0].Length - i; j++)
					newList[^1].Add(input2[^(j + 1)][i + j]);
			}
			newList.Insert(0, input[0]);
#if DEBUG
			var decoded = DecodingI.DecodeTraversal(newList.GetSlice(1).JoinIntoSingle().ToList(x => new ShortIntervalList(x)), 4, input2[0].Length, input2.Length);
			for (var i = 0; i < input2.Length && i < decoded.Length; i++)
			{
				for (var j = 0; j < input2[i].Length && j < decoded[i].Length; j++)
				{
					var x = input2[i][j];
					var y = decoded[i][j];
					if (!RedStarLinq.Equals(x, y)) throw new DecoderFallbackException();
				}
			}
			if (input2.Length * input2[0].Length != decoded.Sum(x => x.Length))
				throw new DecoderFallbackException();
#endif
			return newList;
		}
		else if (mode == TraversalMode.Diagonal2)
		{
			input.Reverse(1, input.Length - 1);
			return input.Traverse(TraversalMode.Diagonal);
		}
		else if (mode == TraversalMode.Spiral)
		{
			var input2 = input.GetSlice(1);
			if (input2.Length <= 1 || input2[0].Length <= 1)
				return input;
			List<List<ShortIntervalList>> newList = [];
			List<(int X, int Y)> start = [(0, 0), (input2[0].Length - 1, 1), (input2[0].Length - 2, input2.Length - 1), (0, input2.Length - 2)];
			List<int> length = [input2[0].Length, input2.Length - 1, input2[0].Length - 1, input2.Length - 2];
			List<(int X, int Y)> direction = [(1, 0), (0, 1), (-1, 0), (0, -1)];
			List<(int X, int Y)> reduction = [(1, 1), (-1, 1), (-1, -1), (1, -1)];
			while (length[0] > 0 && length[1] > 0)
			{
				for (var i = 0; i < 4; i++)
				{
					if (length[i] <= 0)
						break;
					newList.Add([]);
					for (int j = start[i].Y, k = start[i].X, k2 = 0; k2 < length[i]; j += direction[i].Y, k += direction[i].X, k2++)
						newList[^1].Add(input2[j][k]);
					start[i] = (start[i].X + reduction[i].X, start[i].Y + reduction[i].Y);
					length[i] -= 2;
				}
			}
			if (length[0] > 0 && length[1] >= 0)
			{
				newList.Add([]);
				for (int j = start[0].Y, k = start[0].X, k2 = 0; k2 < length[0]; j += direction[0].Y, k += direction[0].X, k2++)
					newList[^1].Add(input2[j][k]);
			}
			newList.Insert(0, input[0]);
#if DEBUG
			var decoded = DecodingI.DecodeTraversal(newList.GetSlice(1).JoinIntoSingle().ToList(x => new ShortIntervalList(x)), 8, input2[0].Length, input2.Length);
			for (var i = 0; i < input2.Length && i < decoded.Length; i++)
			{
				for (var j = 0; j < input2[i].Length && j < decoded[i].Length; j++)
				{
					var x = input2[i][j];
					var y = decoded[i][j];
					if (!RedStarLinq.Equals(x, y)) throw new DecoderFallbackException();
				}
			}
			if (input2.Length * input2[0].Length != decoded.Sum(x => x.Length))
				throw new DecoderFallbackException();
#endif
			return newList;
		}
		else
			return input;
	}

	public static List<List<T>> Enline<T>(this List<List<T>> input)
	{
		var rev = true;
		foreach (var list in input.GetSlice(1))
		{
			if (rev = !rev)
				list.Reverse();
		}
		return input;
	}

	private static List<ShortIntervalList> RLE(this List<ShortIntervalList> input, int tn, bool cout = false)
	{
		List<ShortIntervalList> result = new(input.Length);
		int length = 1, startPos = 3;
		result.AddRange(input.GetSlice(0, startPos));
		List<ShortIntervalList> uniqueLists;
		NList<int> indexCodes;
		if (cout)
			(uniqueLists, indexCodes) = input.GetSlice(startPos).Wrap(dl => (dl.RemoveDoubles(x => x[0]), dl.RepresentIntoNumbers((x, y) => x[0].Equals(y[0]), x => x[0].GetHashCode())));
		else
			(uniqueLists, indexCodes) = input.GetSlice(startPos).Wrap(dl => (dl.RemoveDoubles(x => (x[0], x[1], x[2], x[3])), dl.RepresentIntoNumbers((x, y) => x[0].Equals(y[0]) && x[1].Equals(y[1]) && x[2].Equals(y[2]) && x[3].Equals(y[3]), x => HashCode.Combine(x[0], x[1], x[2], x[3]))));
		var prev = indexCodes[0];
		var prevEqual = false;
		var doNotReadEqual = false;
		Status[tn] = 0;
		StatusMaximum[tn] = indexCodes.Length;
		for (var i = 1; i < indexCodes.Length; i++)
		{
			if (prevEqual)
			{
				if (indexCodes[i] == prev)
				{
					length++;
					if (length >= 65536)
					{
						result.Add(new(uniqueLists[indexCodes[i]].Concat(new ShortIntervalList { new((ValuesInByte >> 1) - 1, ValuesInByte), new((uint)((length - (ValuesInByte >> 1)) >> BitsPerByte), ValuesInByte), new((byte)(length - (ValuesInByte >> 1)), ValuesInByte) })));
						length = 0;
						prevEqual = false;
						doNotReadEqual = true;
						if (i > indexCodes.Length - 1)
							break;
					}
				}
				else
				{
					result.Add(new(uniqueLists[prev].Concat(length >= ValuesInByte >> 1 ? [new Interval((ValuesInByte >> 1) - 1, ValuesInByte), new((uint)((length - (ValuesInByte >> 1)) >> BitsPerByte), ValuesInByte), new((byte)(length - (ValuesInByte >> 1)), ValuesInByte)] : [new((uint)(length - 1), ValuesInByte)])));
					length = 1;
					prevEqual = false;
				}
			}
			else
			{
				if (indexCodes[i] == prev && !doNotReadEqual)
				{
					if (length >= 2)
					{
						result.Add(new(uniqueLists[indexCodes[i - length]].Concat(length >= ValuesInByte >> 1 ? [new Interval(ValuesInByte - 1, ValuesInByte), new((uint)((length - (ValuesInByte >> 1)) >> BitsPerByte), ValuesInByte), new((byte)(length - (ValuesInByte >> 1)), ValuesInByte)] : [new((uint)(length + ((ValuesInByte >> 1) - 1)), ValuesInByte)])));
						for (var j = i - length + 1; j <= i - 2; j++)
							result.Add(uniqueLists[indexCodes[j]]);
					}
					length = 2;
					prevEqual = true;
				}
				else
				{
					length++;
					doNotReadEqual = false;
					if (length >= 65536)
					{
						result.Add(new(uniqueLists[indexCodes[i - length + 1]].Concat(new ShortIntervalList { new(ValuesInByte - 1, ValuesInByte), new((uint)((length - (ValuesInByte >> 1)) >> BitsPerByte), ValuesInByte), new((byte)(length - (ValuesInByte >> 1)), ValuesInByte) })));
						for (var j = i - length + 2; j <= i; j++)
							result.Add(uniqueLists[indexCodes[j]]);
						length = 0;
						if (i > indexCodes.Length - 1)
							break;
					}
				}
			}
			prev = indexCodes[i];
			Status[tn]++;
		}
		if (prevEqual)
			result.Add(new(uniqueLists[indexCodes[^1]].Concat(length >= ValuesInByte >> 1 ? [new Interval((ValuesInByte >> 1) - 1, ValuesInByte), new((uint)((length - (ValuesInByte >> 1)) >> BitsPerByte), ValuesInByte), new((byte)(length - (ValuesInByte >> 1)), ValuesInByte)] : [new((uint)(length - 1), ValuesInByte)])));
		else
		{
			if (length != 0)
			{
				result.Add(new(uniqueLists[indexCodes[^length]].Concat(length >= (ValuesInByte >> 1) - 1 ? [new Interval(ValuesInByte - 1, ValuesInByte), new((uint)((length - ((ValuesInByte >> 1) - 1)) >> BitsPerByte), ValuesInByte), new((byte)(length - ((ValuesInByte >> 1) - 1)), ValuesInByte)] : [new((uint)(length + (ValuesInByte >> 1)), ValuesInByte)])));
				for (var j = indexCodes.Length - length + 1; j <= indexCodes.Length - 1; j++)
					result.Add(uniqueLists[indexCodes[j]]);
			}
		}
		result[1] = new(result[1]);
		result[1][^1] = new(result[1][^1].Lower / 2 * 2 + 1, 24);
		return result.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length))) < input.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length))) ? input.Replace(result) : input;
	}

	private static List<ShortIntervalList> Huffman(this List<ShortIntervalList> input, int tn, bool cout = false)
	{
		if (input.Length == 0)
			return [];
		var bwtIndex = input[0].IndexOf(BWTApplied);
		if (CreateVar(input[0].IndexOf(HuffmanApplied), out var huffmanIndex) != -1 && (bwtIndex == -1 || huffmanIndex != bwtIndex + 1))
			return input;
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		List<ShortIntervalList> result = new(input);
		result[0] = new(result[0]);
		var lz = CreateVar(input[0].IndexOf(LempelZivApplied), out var lzIndex) != -1 && (bwtIndex == -1 || lzIndex != bwtIndex + 1);
		var lzDummy = CreateVar(input[0].IndexOf(LempelZivDummyApplied), out var lzDummyIndex) != -1 && (bwtIndex == -1 || lzDummyIndex != bwtIndex + 1);
		var bwtLength = bwtIndex != -1 ? (int)input[0][bwtIndex + 1].Base : 0;
		var startPos = (lz || lzDummy ? (input[0].Length >= lzIndex + 2 && input[0][lzIndex + 1] == LempelZivSubdivided ? 4 : 3) : 2) + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? 1 : 0) + (cout ? 0 : 1) + bwtLength;
		var lzPos = bwtIndex != -1 ? 4 : 2;
		if (input.Length < startPos + 2)
			return input;
		var spaces = input[0].Length >= 2 && input[0][1] == SpacesApplied;
		var innerCount = spaces ? 2 : 1;
		Status[tn] = 0;
		StatusMaximum[tn] = 7;
		var maxFrequency = 1;
		var groups = input.GetSlice(startPos).ToNList((x, index) => (elem: lz && index >= 2 && x[0].Lower + x[0].Length == x[0].Base || cout || bwtIndex != -1 ? (x[0], Interval.Default, Interval.Default, Interval.Default) : (x[0], x[1], x[2], x[3]), index)).Wrap(l => lz ? l.FilterInPlace(x => x.index < 2 || x.elem.Item1.Lower + x.elem.Item1.Length != x.elem.Item1.Base) : l).Group(x => x.elem).Wrap(l => CreateVar(l.Max(x => x.Length), out maxFrequency) > input[startPos][0].Base * 2 || input[startPos][0].Base <= ValuesInByte ? l.NSort(x => 4294967295 - (uint)x.Length) : l);
		Status[tn]++;
		var uniqueList = groups.PConvert(x => (new Interval(x[0].elem.Item1) { Base = input[startPos][0].Base }, x[0].elem.Item2, x[0].elem.Item3, x[0].elem.Item4));
		Status[tn]++;
		var indexCodes = RedStarLinq.NEmptyList<int>(input.Length - startPos);
		for (var i = 0; i < groups.Length; i++)
			foreach (var (elem, index) in groups[i])
				indexCodes[index] = i;
		Status[tn]++;
		NList<(int elem, int freq)> frequencyTable = groups.PNConvert((x, index) => (index, x.Length));
		groups.Dispose();
		Status[tn]++;
		var frequency = frequencyTable.PNConvert(x => x.freq);
		Status[tn]++;
		var intervalsBase = (uint)frequency.Sum();
		uint a = 0;
		var arithmeticMap = frequency.ToNList(x => a += (uint)x);
		Status[tn]++;
		if (lz)
			intervalsBase = GetBaseWithBuffer(arithmeticMap[^1]);
		var frequencyIntervals = arithmeticMap.Prepend(0u).GetSlice(0, arithmeticMap.Length).ToNList((x, index) => new Interval(x, (uint)frequency[index], intervalsBase));
		Status[tn]++;
		Interval lzInterval = lz ? new(arithmeticMap[^1], intervalsBase - arithmeticMap[^1], intervalsBase) : new();
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length - startPos;
		Current[tn] += ProgressBarStep;
		Parallel.For(startPos, input.Length, i =>
		{
			if (lz && i >= startPos + lzPos && result[i][0].Lower + result[i][0].Length == result[i][0].Base)
				result[i] = new(result[i]) { [0] = lzInterval };
			else
				result[i] = new(result[i]) { [0] = i >= startPos + lzPos ? frequencyIntervals[indexCodes[i - startPos]] : new(frequencyIntervals[indexCodes[i - startPos]]) { Base = arithmeticMap[^1] } };
			if (!(lz && result[i][0].Lower + result[i][0].Length == result[i][0].Base || cout || bwtIndex != -1))
			{
				result[i].RemoveAt(3);
				result[i].RemoveAt(2);
				result[i].RemoveAt(1);
			}
			Status[tn]++;
		});
		indexCodes.Dispose();
		arithmeticMap.Dispose();
		frequencyIntervals.Dispose();
		Status[tn]++;
		NList<Interval> c = [];
		c.WriteCount((uint)maxFrequency - 1);
		c.WriteCount((uint)frequencyTable.Length - 1);
		Status[tn] = 0;
		StatusMaximum[tn] = frequencyTable.Length;
		Current[tn] += ProgressBarStep;
		if (maxFrequency > input[startPos][0].Base * 2 || input[startPos][0].Base <= ValuesInByte)
			for (var i = 0; i < frequencyTable.Length; i++, Status[tn]++)
			{
				var value = uniqueList[frequencyTable[i].elem];
				c.Add(value.Item1);
				c.Add(value.Item2);
				c.Add(value.Item3);
				c.Add(value.Item4);
				if (i != 0)
					c.Add(new((uint)frequency[i] - 1, (uint)frequency[i - 1]));
			}
		else
			for (var i = 0; i < frequencyTable.Length; i++, Status[tn]++)
				c.Add(new(frequency[i] >= 1 ? (uint)frequency[i] - 1 : throw new EncoderFallbackException(), (uint)maxFrequency));
		uniqueList.Dispose();
		frequencyTable.Dispose();
		frequency.Dispose();
		var cSplit = c.SplitIntoEqual(8);
		c.Dispose();
		var cLength = (uint)cSplit.Length;
		var insertIndex = lz ? lzIndex : result[0].Length;
		result[0].Insert(insertIndex, HuffmanApplied);
		result[0].Insert(insertIndex + 1, new(0, cLength, cLength));
		result.Insert(startPos - bwtLength, cSplit.PConvert(x => new ShortIntervalList(x)));
		cSplit.Dispose();
		result[1] = new(result[1]);
		result[1][^1] = new(result[1][^1].Lower / 8 * 8 + result[1][^1].Lower % 2 + 2, 24);
		return result.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length))) < input.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length))) ? input.Replace(result) : input;
	}

	private static List<ShortIntervalList> AdaptiveHuffman(this List<ShortIntervalList> input, LZData lzData, int tn, bool cout = false)
	{
		if (input.Length < 2)
			throw new EncoderFallbackException();
		var result = new AdaptiveHuffmanI(tn).Encode(input, lzData, cout);
		result.Add([new(1234567890, 4294967295)]);
		return result;
	}

	private static List<ShortIntervalList> LempelZiv(this List<ShortIntervalList> input, out LZData lzData, int tn, bool cout = false)
	{
		var result = new LempelZiv(input, [], tn, cout).Encode(out lzData);
		result[1] = new(result[1]);
		result[1][^1] = new(result[1][^1].Lower % 8 + 8, 24);
		return result.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length))) < input.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length))) ? input.Replace(result) : input;
	}

	public static double StandardDeviation(this G.IEnumerable<double> list)
	{
		if (!list.Any())
			return 0;
		var mean = list.Mean();
		var sum = list.Convert(x => (x - mean) * (x - mean)).Sum();
		return Sqrt(sum / list.Length());
	}

	private static void Encode1(List<List<Rgba32>> input, int n, ref byte[] cs, out List<ShortIntervalList> cdl, int tn)
	{
		const int methodsCount = 9;
		var tl = input.ToTripleList();
		var sum = tl.Sum(dl => dl.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length)))) / Log(256);
		cdl = default!;
		bool hf = PresentMethodsI.HasFlag(tn switch { 0 => UsedMethodsI.HF1, 1 => UsedMethodsI.HF2, 2 => UsedMethodsI.CS3, 3 => UsedMethodsI.CS4, _ => throw new EncoderFallbackException() }), bwt = n >= 2, lz = PresentMethodsI.HasFlag(tn switch { 0 => UsedMethodsI.LZ1, 1 => UsedMethodsI.LZ2, 2 => UsedMethodsI.LZ3, 3 => UsedMethodsI.LZ4, _ => throw new EncoderFallbackException() }) && !bwt;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * methodsCount * 3;
		for (var tlIndex = 0; tlIndex < 1; tlIndex++)
		{
			for (var i = 0; i < methodsCount; i++)
			{
				Current[tn] = 0;
				CurrentMaximum[tn] = ProgressBarStep * 6;
				GC.Collect();
				var tl1 = tl.ToList(dl => dl.ToList(l => new ShortIntervalList(l)));
				tl1[0][1][^2] = new((uint)(n * methodsCount + i), methodsCount << 2);
				var traversedList = tl1.Traverse((TraversalMode)(i / 2));
				Current[tn] += ProgressBarStep;
				if (i % 2 == 1)
					traversedList.Enline();
				Current[tn] += ProgressBarStep;
				var dl1 = traversedList.JoinIntoSingle().Wrap(x => n < 4 ? x.Alpha() : x);
				Current[tn] += ProgressBarStep;
				if (n % 2 == 1)
					dl1.Delta();
				Current[tn] += ProgressBarStep;
				Current[tn] += ProgressBarStep;
				dl1.RLE(tn, n >= 4);
				Current[tn] += ProgressBarStep;
				Subtotal[tn] += ProgressBarStep;
				if (bwt)
				{
					var enoughTransparency = dl1.GetSlice(3).Count(l => l[1].Base == 1 && l[2].Base == 1 && l[3].Base == 1) >= dl1.Length * 0.03;
					dl1[2].Add(new(enoughTransparency ? 1u : 0, 2));
					dl1 = new BWT(tn).Encode(dl1.GetSlice(0, 3).Concat(dl1.GetSlice(3).ConvertAndJoin(l => (dl1[2][0].Lower == 0 ? l.GetSlice(1) : enoughTransparency && l[1].Base == 1 && l[2].Base == 1 && l[3].Base == 1 ? l.GetSlice(4).Prepend(l[0]) : l.GetSlice()).Convert(x => new ShortIntervalList([new(x.Lower, x.Length, ValuesInByte)])))), (int)(dl1[1][^2].Lower / methodsCount % 2), enoughTransparency);
				}
				//var s2 = WorkUpDoubleList(PPM(dl1, tn, n >= 4), tn);
				//ctl[tlIndex] = dl1;
				if ((PresentMethodsI & UsedMethodsI.AHF) != 0)
				{
					LZData lzData = new();
					if (lz) dl1.LempelZiv(out lzData, tn, n >= 4);
					Subtotal[tn] += ProgressBarStep;
					if (hf) dl1.AdaptiveHuffman(lzData, tn, n >= 4);
				}
				else
				{
					if (hf) dl1.Huffman(tn, n >= 4);
					Subtotal[tn] += ProgressBarStep;
					if (lz) dl1.LempelZiv(out var lzData, tn, n >= 4);
				}
				var s1 = dl1.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length))) / Log(256);
				if (s1 > 0 && (s1 < sum || sum == 0))
				{
					cdl = dl1;
					sum = s1;
				}
				Subtotal[tn] += ProgressBarStep;
			}
		}
		cs = WorkUpDoubleList(cdl ?? tl.JoinIntoSingle(), tn);
	}

	public static byte[] Encode(Image<Rgba32> originalImage, byte[] originalBytes, out List<ShortIntervalList> intervals)
	{
		var s = RedStarLinq.FillArray(ProgressBarGroups, _ => originalBytes);
		var cs = originalBytes;
		var tl = RedStarLinq.FillArray(ProgressBarGroups, _ => (List<ShortIntervalList>)default!);
		int lw = 0, part = 0, lwP5 = 0;
		List<List<Rgba32>> input = [];
		Total = 0;
		TotalMaximum = ProgressBarStep * 6;
		for (var iy = 0; iy < originalImage.Height; iy++)
		{
			input.Add([]);
			for (var ix = 0; ix < originalImage.Width; ix++)
				input[^1].Add(originalImage[ix, iy]);
		}
		widthAndHeightIntervals = GetWidthAndHeightIntervals(input);
		Total += ProgressBarStep;
		Threads[0] = new(() =>
		{
			try
			{
				if ((PresentMethodsI & UsedMethodsI.CS1) != 0)
					Encode1(input, 0, ref s[0], out tl[0], 0);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[1] = new(() =>
		{
			try
			{
				if ((PresentMethodsI & UsedMethodsI.CS2) != 0)
					Encode1(input, 1, ref s[1], out tl[1], 1);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[2] = new(() =>
		{
			try
			{
				if ((PresentMethodsI & UsedMethodsI.CS3) != 0)
					Encode1(input, 2, ref s[2], out tl[2], 2);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[3] = new(() =>
		{
			try
			{
				if ((PresentMethodsI & UsedMethodsI.CS4) != 0)
					Encode1(input, 3, ref s[3], out tl[3], 3);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[4] = new(() =>
		{
			try
			{
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		for (var i = 0; i < ProgressBarGroups; i++)
			if (Threads[i] != null && Threads[i].ThreadState is not System.Threading.ThreadState.Unstarted or System.Threading.ThreadState.Running)
				Threads[i] = default!;
		Threads[0].Name = "Encode1-1";
		Threads[1].Name = "Encode1-2";
		Threads[2].Name = "Encode1-3";
		Threads[3].Name = "Encode1-4";
		Threads[4].Name = "LWEncode";
		Threads.ForEach(x => _ = x == null || (x.IsBackground = true));
		Thread.CurrentThread.Priority = ThreadPriority.Lowest;
		Threads.ForEach(x => x?.Start());
		Threads.ForEach(x => x?.Join());
		//if ((PresentMethods & UsedMethods.CS7) != 0 && s[6].Length < cs.Length && s[6].Length > 0 && s.GetSlice(0, 6).All(x => s[6].Length < x.Length))
		//{
		//	cs = s[6];
		//}
		//else if ((PresentMethods & UsedMethods.CS6) != 0 && s[5].Length < cs.Length && s[5].Length > 0 && s.GetSlice(0, 5).All(x => s[5].Length < x.Length))
		//{
		//	cs = s[5];
		//}
		var bestIndex = s.Prepend(cs).IndexOfMin(x => CreateVar(x.Length, out var sum) == 0 ? double.PositiveInfinity : sum) - 1;
		if ((PresentMethodsI & UsedMethodsI.CS6) != 0 && bestIndex == 4)
		{
			lw = lwP5;
			part = 8;
			cs = s[4];
			intervals = tl[4];
		}
		else if ((PresentMethodsI & UsedMethodsI.CS4) != 0 && bestIndex == 3)
		{
			part = 0;
			cs = s[3];
			intervals = tl[3];
		}
		else if ((PresentMethodsI & UsedMethodsI.CS3) != 0 && bestIndex == 2)
		{
			part = 0;
			cs = s[2];
			intervals = tl[2];
		}
		else if ((PresentMethodsI & UsedMethodsI.CS2) != 0 && bestIndex == 1)
		{
			part = 0;
			cs = s[1];
			intervals = tl[1];
		}
		else if ((PresentMethodsI & UsedMethodsI.CS1) != 0 && bestIndex == 0)
		{
			part = 0;
			cs = s[0];
			intervals = tl[0];
		}
		else
		{
			intervals = originalBytes.ToList(x => ByteIntervals[x]);
			return [0, .. originalBytes];
		}
		var compressedFile = new byte[] { ProgramVersion, (byte)(lw + part) }.Concat(cs).ToArray();
#if DEBUG
		try
		{
			using var decoded = DecodingI.Decode(compressedFile, out _);
			for (var i = 0; i < originalImage.Height; i++)
				for (var j = 0; j < originalImage.Width; j++)
					if (originalImage[j, i] != decoded[j, i] && !(originalImage[j, i].A == 0 && decoded[j, i].A == 0)) throw new DecoderFallbackException();
		}
		catch
		{
			throw new DecoderFallbackException();
		}
#endif
		return compressedFile;
	}

	public static byte[] Encode(Image<Rgba32> originalImage, byte[] originalBytes) => Encode(originalImage, originalBytes, out _);
}
