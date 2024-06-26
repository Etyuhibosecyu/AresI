using System.Diagnostics;

namespace AresILib;

public static partial class Executions
{
	private const int LZDictionarySize = 32767;
	private static ShortIntervalList widthAndHeightIntervals = [];

	private static List<ShortIntervalList> Alpha(this List<ShortIntervalList> input)
	{
		var input2 = input.AsSpan(2);
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

	private static List<List<ShortIntervalList>> ToTripleList(this List<List<Bgra32>> input)
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

	private static List<List<List<ShortIntervalList>>> ToQuadrupleList(this List<List<Bgra32>> input)
	{
		var result = RedStarLinq.Fill(4, index => new List<List<ShortIntervalList>>(input.Length + 1) { new() { new() { PixelsApplied }, index == 0 ? new(widthAndHeightIntervals) { new(0, 36), new(0, 12) } : new() { new(0, 36), new(0, 16) } } });
		for (var i = 0; i < input.Length; i++)
		{
			result.ForEach(x => x.Add(new(input[i].Length)));
			for (var j = 0; j < input[i].Length; j++)
			{
				result.ForEach(x => x[^1].Add([]));
				var color = input[i][j];
				result[0][^1][^1].Add(new(color.A, ValuesInByte));
				result[1][^1][^1].Add(new((byte)((color.R + (color.G << 1) + color.B) >> 2), ValuesInByte));
				result[2][^1][^1].Add(new((byte)(color.R + (ValuesInByte >> 1) - color.G), ValuesInByte));
				result[3][^1][^1].Add(new((byte)(color.B + (ValuesInByte >> 1) - color.G), ValuesInByte));
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

	public static List<List<T>> Traverse<T>(this List<List<T>> input, TraversalMode mode)
	{
		if (mode == TraversalMode.Table)
			return input;
		else if (mode == TraversalMode.TableV)
			return input[1..].Transpose().Insert(0, input[0]);
		else if (mode == TraversalMode.Diagonal)
		{
			var input2 = input.AsSpan(1);
			List<List<T>> newList = [];
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
			return newList;
		}
		else if (mode == TraversalMode.Diagonal2)
		{
			input.Reverse(1, input.Length - 1);
			return input.Traverse(TraversalMode.Diagonal);
		}
		else if (mode == TraversalMode.Spiral)
		{
			var input2 = input.AsSpan(1);
			if (input2.Length <= 1 || input2[0].Length <= 1)
				return input;
			List<List<T>> newList = [];
			List<(int X, int Y)> start = [(0, 0), (input2[0].Length - 1, 1), (input2[0].Length - 2, input2.Length - 1), (0, input2.Length - 2)];
			List<int> length = [input2[0].Length, input2.Length - 1, input2[0].Length - 1, input2.Length - 2];
			List<(int X, int Y)> direction = [(1, 0), (0, 1), (-1, 0), (0, -1)];
			List<(int X, int Y)> reduction = [(1, 1), (-1, 1), (-1, -1), (1, -1)];
			while (length[0] > 0 && length[1] > 0)
			{
				for (var i = 0; i < 4; i++)
				{
					if (length[i] <= 0)
						continue;
					newList.Add([]);
					for (int j = start[i].Y, k = start[i].X, k2 = 0; k2 < length[i]; j += direction[i].Y, k += direction[i].X, k2++)
						newList[^1].Add(input2[j][k]);
					start[i] = (start[i].X + reduction[i].X, start[i].Y + reduction[i].Y);
					length[i] -= 2;
				}
			}
			newList.Insert(0, input[0]);
			return newList;
		}
		else
			return input;
	}

	public static List<List<T>> Enline<T>(this List<List<T>> input)
	{
		var rev = true;
		foreach (var list in input.AsSpan(1))
		{
			if (rev = !rev)
				list.Reverse();
		}
		return input;
	}

	private static (List<ShortIntervalList> Result, List<ShortIntervalList> Internal) LosslessWavelet(List<List<ShortIntervalList>> input, ref byte[] cs, ref int indicator, int tn)
	{
		List<ShortIntervalList> result = new(input.Length * input[1].Length);
		Interval default_ = new(0, ValuesInByte);
		var pairsList = input.GetSlice(1).Convert(dl => dl.Group((x, index) => index / 2).Convert(l => l[0].Combine(l.Length == 1 ? new(l[0].Convert(x => default_)) : l[1]))).Group((x, index) => index / 2).Convert(dl => dl[0].Combine(dl.Length == 1 ? dl[0].Convert(x => x.Convert(y => (default_, default_))) : dl[1].GetSlice()).Convert(l => l.Item1.Combine(l.Item2)));
		List<List<List<ShortIntervalList>>> matrices = [pairsList.ToList(dl => dl.ToList(l => new ShortIntervalList(l.ToList(x => new Interval((x.Item1.Item1.Lower + x.Item1.Item2.Lower + x.Item2.Item1.Lower + x.Item2.Item2.Lower) / 4, x.Item1.Item1.Base))))), pairsList.ToList(dl => dl.ToList(l => new ShortIntervalList(l.ToList(x => new Interval((x.Item1.Item2.Lower + x.Item1.Item1.Base * 3 / 2 - x.Item1.Item1.Lower) % x.Item1.Item1.Base, x.Item1.Item2.Base))))), pairsList.ToList(dl => dl.ToList(l => new ShortIntervalList(l.ToList(x => new Interval((x.Item2.Item1.Lower + x.Item1.Item1.Base * 3 / 2 - x.Item1.Item1.Lower) % x.Item1.Item1.Base, x.Item2.Item1.Base))))), pairsList.ToList(dl => dl.ToList(l => new ShortIntervalList(l.ToList(x => new Interval((x.Item2.Item2.Lower + x.Item1.Item1.Base * 3 / 2 - x.Item1.Item2.Lower) % x.Item1.Item1.Base, x.Item2.Item2.Base)))))];
		matrices[0].Insert(0, new List<ShortIntervalList>() { new() { PixelsApplied, LWApplied }, new() { new(0, 12) }, new() });
		List<uint> newBufferLower = [], newBufferLower2 = [];
		var @internal = matrices.ConvertAndJoin(tl => tl).ConvertAndJoin(dl => dl).ToList();
		var newMatrices = matrices.ToList((tl, index) => tl.ConvertAndJoin(dl => dl).ToList().Wrap(dl => index == 0 ? dl : dl.Insert(0, new List<ShortIntervalList>() { new() { PixelsApplied, LWApplied }, new() { new(0, 12) }, new() })).RLE(tn).LW_Huffman(tn).LempelZiv(tn));
		var newMatrices2 = new List<List<ShortIntervalList>>() { matrices[0].ConvertAndJoin(dl => dl).ToList(), matrices.AsSpan(1).ConvertAndJoin(tl => tl).ConvertAndJoin(dl => dl).ToList().Insert(0, new List<ShortIntervalList>() { new() { PixelsApplied, LWApplied }, new() { new(0, 12) }, new() }).RLE(tn).LW_Huffman(tn).LempelZiv(tn) };
		var reducedNewMatrices = newMatrices.ConvertAndJoin(dl => dl).ToList();
		var reducedNewMatrices2 = newMatrices2.ConvertAndJoin(dl => dl).ToList();
		Subtotal[tn] += ProgressBarStep;
		var s = WorkUpDoubleList(reducedNewMatrices, tn);
		Subtotal[tn] += ProgressBarStep;
		var s2 = WorkUpDoubleList(reducedNewMatrices2, tn);
		Subtotal[tn] += ProgressBarStep;
		var s3 = WorkUpDoubleList(newMatrices[0], tn);
		List<List<List<ShortIntervalList>>> cNewMatrices;
		List<ShortIntervalList> cReducedNewMatrices;
		indicator++;
		if (s.Length < cs.Length && s.Length < s2.Length)
		{
			cs = s;
			cNewMatrices = newMatrices;
			cReducedNewMatrices = reducedNewMatrices;
		}
		else if (s2.Length < cs.Length)
		{
			cs = s2;
			cNewMatrices = newMatrices2;
			cReducedNewMatrices = reducedNewMatrices2;
		}
		else
		{
			var reducedInput = input.ConvertAndJoin(dl => dl).ToList();
			return (reducedInput, reducedInput);
		}
		if (indicator < 6)
		{
			Subtotal[tn] += ProgressBarStep;
			matrices[0] = [LosslessWavelet(matrices[0], ref s3, ref indicator, tn).Internal];
			newMatrices = matrices.ToList((tl, index) => tl.ConvertAndJoin(dl => dl).ToList().Wrap(dl => index == 0 ? dl : dl.Insert(0, new List<ShortIntervalList>() { new() { PixelsApplied, LWApplied }, new() { new(0, 12) }, new() })).RLE(tn).LW_Huffman(tn).LempelZiv(tn));
			newMatrices2 = [matrices[0].ConvertAndJoin(dl => dl).ToList().RLE(tn).LW_Huffman(tn).LempelZiv(tn), matrices.AsSpan(1).ConvertAndJoin(tl => tl).ConvertAndJoin(dl => dl).ToList().Insert(0, new List<ShortIntervalList>() { new() { PixelsApplied, LWApplied }, new() { new(0, 12) }, new() }).RLE(tn).LW_Huffman(tn).LempelZiv(tn)];
			reducedNewMatrices = newMatrices.ConvertAndJoin(dl => dl).ToList();
			reducedNewMatrices2 = newMatrices2.ConvertAndJoin(dl => dl).ToList();
			s = WorkUpDoubleList(reducedNewMatrices, tn);
			s2 = WorkUpDoubleList(reducedNewMatrices2, tn);
			(cReducedNewMatrices, cs) = s.Length < s2.Length ? (reducedNewMatrices, s) : (reducedNewMatrices2, s2);
		}
		else
			Subtotal[tn] += ProgressBarStep;
		result.AddRange(cReducedNewMatrices);
		Subtotal[tn] += ProgressBarStep;
		return (result, @internal);
	}

	private static List<ShortIntervalList> LW_RLE(List<ShortIntervalList> input, int tn) => RLE(input, tn, false);

	private static List<ShortIntervalList> LW_Huffman(this List<ShortIntervalList> input, int tn)
	{
		Status[tn] = 0;
		StatusMaximum[tn] = 10;
		var input2 = input.GetSlice(3);
		var uniqueLists = input2.ConvertAndJoin(l => l.Take(4)).ToHashSet();
		Status[tn]++;
		var indexCodes = input2.Convert(l => l.Take(4).Convert(x => uniqueLists.IndexOf(x)));
		Status[tn]++;
		var reducedIndexCodes = indexCodes.ConvertAndJoin(l => l);
		Status[tn]++;
		var frequency = reducedIndexCodes.Group().Convert(x => (uint)x.Length);
		Status[tn]++;
		var maxFrequency = frequency.Max();
		Status[tn]++;
		var frequencySum = (uint)frequency.Sum(x => (int)x);
		Status[tn]++;
		uint a = 0;
		var arithmeticMap = frequency.ToNList(x => a += x);
		Status[tn]++;
		var base_ = frequencySum + Max((frequencySum + 10) / 20, 1);
		var frequencyIntervals = arithmeticMap.Prepend(0u).Take(arithmeticMap.Length).ToNList((x, index) => new Interval(x, frequency[index], base_));
		Status[tn]++;
		var result = indexCodes.ToList((l, lIndex) => new ShortIntervalList(l.Convert((x, index) => frequencyIntervals[x]).Concat(input[lIndex + 3].Skip(4)))).Insert(0, input[..3]);
		result[0] = new(result[0]);
		Status[tn]++;
		var c = new List<Interval>();
		c.WriteCount(maxFrequency - 1);
		frequency.ConvertAndJoin((x, index) => new ShortIntervalList { uniqueLists[index], new(x - 1, maxFrequency) }).ForEach(x => c.Add(x));
		var cSplit = c.SplitIntoEqual(8);
		c.Dispose();
		var cLength = (uint)cSplit.Length;
		var insertIndex = result[0].Length;
		result[0].Insert(insertIndex, HuffmanApplied);
		result[0].Insert(insertIndex + 1, new(0, cLength, cLength));
		result.Insert(3, cSplit.PConvert(x => new ShortIntervalList(x)));
		cSplit.Dispose();
		Status[tn]++;
		return result;
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
		List<Interval> c = [];
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
		var result = AdaptiveHuffmanInternal(input, lzData, tn, cout);
		result.Add([new(1234567890, 4294967295)]);
		return result;
	}

	private static List<ShortIntervalList> AdaptiveHuffmanInternal(this List<ShortIntervalList> input, LZData lzData, int tn, bool cout = false)
	{
		var bwtIndex = input[0].IndexOf(BWTApplied);
		if (CreateVar(input[0].IndexOf(HuffmanApplied), out var huffmanIndex) != -1 && !(bwtIndex != -1 && huffmanIndex == bwtIndex + 1))
			throw new EncoderFallbackException();
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		Status[tn] = 0;
		StatusMaximum[tn] = 3;
		var lz = CreateVar(input[0].IndexOf(LempelZivApplied), out var lzIndex) != -1 && (bwtIndex == -1 || lzIndex != bwtIndex + 1);
		var lzDummy = CreateVar(input[0].IndexOf(LempelZivDummyApplied), out var lzDummyIndex) != -1 && (bwtIndex == -1 || lzDummyIndex != bwtIndex + 1);
		var bwtLength = bwtIndex != -1 ? (int)input[0][bwtIndex + 1].Base : 0;
		var startPos = (lz || lzDummy ? (input[0].Length >= lzIndex + 2 && input[0][lzIndex + 1] == LempelZivSubdivided ? 4 : 3) : 2) + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? 1 : 0) + (cout ? 0 : 1) + bwtLength;
		Status[tn]++;
		var lzPos = bwtIndex != -1 ? 4 : 2;
		if (input.Length < startPos + lzPos + 1)
			throw new EncoderFallbackException();
		var originalBase = input[startPos + lzPos][0].Base;
		//if (!input.GetSlice(startPos + lzPos + 1).All((x, index) => bwtIndex != -1 && (index + lzPos + 1) % (BWTBlockSize + 2) is 0 or 1 || x[0].Base == originalBase))
		//	throw new EncoderFallbackException();
		Status[tn]++;
		var intervalList = new List<Interval>();
		for (var i = 0; i < startPos; i++)
			for (var j = 0; j < input[i].Length; j++)
			{
				var x = input[i][j];
				//if (i == startPos - bwtLength && j == 2)
				//	intervalList.WriteCount(x.Base);
				intervalList.Add(i == 1 && j == input[i].Length - 1 ? new(x.Lower / 8 * 8 + x.Lower % 2 + 4, 24) : x);
			}
		Status[tn]++;
		if (bwtLength != 0)
			intervalList.WriteCount((uint)(input.Length - startPos));
		var newBase = input[startPos][0].Base + (lz ? 1u : 0);
		uint GetBase(int index) => input.GetSlice(startPos).Find(x => x[index].Base > 1)?[index].Base ?? 1;
		var (base2, base3, base4) = cout || bwtIndex != -1 ? (1, 1, 1) : (GetBase(1), GetBase(2), GetBase(3));
		intervalList.WriteCount(newBase);
		intervalList.WriteCount(base2);
		intervalList.WriteCount(base3);
		intervalList.WriteCount(base4);
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length - startPos;
		Current[tn] += ProgressBarStep;
		SumSet<(uint, uint, uint, uint)> set = [];
		SumList lengthsSL = lz ? new(RedStarLinq.Fill(1, (int)(lzData.Length.R == 0 ? lzData.Length.Max + 1 : lzData.Length.R == 1 ? lzData.Length.Threshold + 2 : lzData.Length.Max - lzData.Length.Threshold + 2))) : new(), distsSL = lz ? new(RedStarLinq.Fill(1, (int)lzData.UseSpiralLengths + 1)) : new();
		var firstIntervalDist = lz ? (lzData.Dist.R == 0 ? lzData.Dist.Max + 1 : lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max - lzData.Dist.Threshold + 2) + lzData.UseSpiralLengths : 0;
		if (lz)
			set.Add(((newBase - 1, 0, 0, 0), 1));
		for (var i = startPos; i < input.Length; i++, Status[tn]++)
		{
			var item = cout || bwtIndex != -1 ? (input[i][0].Lower, 0, 0, 0) : lz && input[i][0].Lower == newBase - 1 ? (input[i][0].Lower, 0, 0, 0) : (input[i][0].Lower, input[i][1].Lower, input[i][2].Lower, input[i][3].Lower);
			var sum = set.GetLeftValuesSum(item, out var frequency);
			var bufferInterval = (uint)Max(set.Length, 1);
			var fullBase = (uint)(set.ValuesSum + bufferInterval);
			if (frequency == 0)
			{
				intervalList.Add(new((uint)set.ValuesSum, bufferInterval, fullBase));
				intervalList.Add(new(item.Item1, newBase));
				intervalList.Add(new(item.Item2, base2));
				intervalList.Add(new(item.Item3, base3));
				intervalList.Add(new(item.Item4, base4));
			}
			else
				intervalList.Add(new((uint)sum, (uint)frequency, fullBase));
			set.Increase(item);
			int lzLength = 0, lzDist = 0, lzSpiralLength = 0;
			var j = cout || bwtIndex != -1 || lz && item.Item1 == newBase - 1 ? 1 : 4;
			if (lz && item.Item1 == newBase - 1)
			{
				var item2 = input[i][j].Lower;
				lzLength = (int)(item2 + (lzData.Length.R == 2 ? lzData.Length.Threshold : 0));
				sum = lengthsSL.GetLeftValuesSum((int)item2, out frequency);
				intervalList.Add(new((uint)sum, (uint)frequency, (uint)lengthsSL.ValuesSum));
				lengthsSL.Increase((int)item2);
				j++;
				if (lzData.Length.R != 0 && item2 == lengthsSL.Length - 1)
				{
					intervalList.Add(new(input[i][j].Lower, input[i][j].Length, input[i][j].Base));
					lzLength = (int)(lzData.Length.R == 2 ? input[i][j].Lower : input[i][j].Lower + lzData.Length.Threshold + 1);
					j++;
				}
				item2 = input[i][j].Lower;
				lzDist = (int)(item2 + (lzData.Dist.R == 2 && distsSL.Length - lzData.UseSpiralLengths - lzLength - startPos >= lzData.Dist.Threshold ? lzData.Dist.Threshold : 0));
				if (lzData.Dist.R == 2 && distsSL.Length - lzData.UseSpiralLengths - lzLength - startPos >= lzData.Dist.Threshold && lzDist == (distsSL.Length == firstIntervalDist ? lzData.Dist.Max : distsSL.Length - lzData.UseSpiralLengths - lzLength - startPos) + 1)
				{
					j++;
					if (input[i][j].Lower != lzData.Dist.Threshold) lzDist = (int)input[i][j].Lower;
				}
				sum = distsSL.GetLeftValuesSum(lzDist, out frequency);
				intervalList.Add(new((uint)sum, (uint)frequency, (uint)distsSL.ValuesSum));
				distsSL.Increase(lzDist);
				j++;
				if (lzData.Dist.R != 0 && input[i][j - 1].Base == firstIntervalDist - lzData.UseSpiralLengths && input[i][j - 1].Lower == input[i][j - 1].Base - 1)
				{
					intervalList.Add(new(input[i][j].Lower, input[i][j].Length, input[i][j].Base));
					j++;
				}
				lzSpiralLength = lzData.UseSpiralLengths != 0 && input[i][j - 1].Lower == input[i][j - 1].Base - 1 ? lzData.SpiralLength.R == 0 ? (int)input[i][^1].Lower : (int)(input[i][^1].Lower + (lzData.SpiralLength.R == 2 != (input[i][^2].Lower == input[i][^2].Base - 1) ? lzData.SpiralLength.Threshold + 2 - lzData.SpiralLength.R : 0)) : 0;
				if (lz && distsSL.Length < firstIntervalDist)
					new Chain(Min((int)firstIntervalDist - distsSL.Length, (lzLength + 2) * (lzSpiralLength + 1))).ForEach(x => distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1));
			}
			else if (lz && distsSL.Length < firstIntervalDist)
				distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1);
			for (; j < input[i].Length; j++)
				intervalList.Add(new(input[i][j].Lower, input[i][j].Length, input[i][j].Base));
		}
		var result = intervalList.SplitIntoEqual(8).PConvert(x => new ShortIntervalList(x));
		return result.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length))) < input.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length))) ? input.Replace(result) : input;
	}

	private static List<ShortIntervalList> LempelZiv(this List<ShortIntervalList> input, int tn, bool cout = false) => LempelZiv(input, out _, tn, cout);

	private static List<ShortIntervalList> LempelZiv(this List<ShortIntervalList> input, out LZData lzData, int tn, bool cout = false)
	{
		var result = new LempelZiv(input, [], tn, cout).Encode(out lzData);
		result[1] = new(result[1]);
		result[1][^1] = new(result[1][^1].Lower % 8 + 8, 24);
		return result.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length))) < input.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length))) ? input.Replace(result) : input;
	}

	private static List<ShortIntervalList> PPM(List<ShortIntervalList> input, int tn, bool cout = false)
	{
		if (input.Length < 4)
			throw new EncoderFallbackException();
		var result = PPMInternal(input, tn, cout);
		return result;
	}

	private static List<ShortIntervalList> PPMInternal(List<ShortIntervalList> input, int tn, bool cout = false)
	{
		if (!(input.Length >= 4 && input[CreateVar(input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base + 1 : 3, out var startPos)].Length >= 1 && input[startPos][0].Length == 1 && CreateVar(input[startPos][0].Base, out var inputBase) >= 1 && input[startPos][^1].Length == 1 && input.GetSlice(startPos + 1).All(x => x[0].Length == 1 && x[0].Base == inputBase && (x.Length == 1 || x[1].Length == 1 && x[1].Base == input[startPos][1].Base))))
			throw new EncoderFallbackException();
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length - startPos;
		var n = 1;
		var intervalList = new List<Interval>();
		for (var i = 0; i < startPos; i++)
			for (var j = 0; j < input[i].Length; j++)
			{
				var x = input[i][j];
				intervalList.Add(i == 1 && j == input[i].Length - 1 ? new(x.Lower / 8 * 8 + x.Lower % 2 + 6, 16) : x);
			}
		var (base2, base3, base4) = cout ? (1, 1, 1) : (input[startPos][1].Base, input[startPos][2].Base, input[startPos][3].Base);
		if (n == 0)
		{
			intervalList.WriteCount(inputBase);
			intervalList.WriteCount(base2);
			intervalList.WriteCount(base3);
			intervalList.WriteCount(base4);
			for (var i = 2; i < startPos; i++)
				for (var j = 0; j < input[i].Length; j++)
					intervalList.Add(input[i][j]);
		}
		intervalList.WriteCount((uint)(input.Length - startPos));
		intervalList.WriteCount(LZDictionarySize);
		SumSet<(uint, uint, uint, uint)>? set = [], excludingSet = [];
		SumSet<(uint, uint, uint, uint)>? set2;
		SumSet<(uint, uint, uint, uint)> globalSet = []/*, newItemsSet = n == 2 ? new() : new(new Chain((int)inputBase).Convert(x => ((uint)x, 1)))*/;
		var maxDepth = inputBase == 2 ? 96 : 12;
		NList<(uint, uint, uint, uint)> context = new(maxDepth), context2 = new(maxDepth);
		LimitedQueue<List<Interval>> buffer = new(maxDepth);
		var comparer = new NListEComparer<(uint, uint, uint, uint)>();
		FastDelHashSet<NList<(uint, uint, uint, uint)>> contextHS = new(comparer);
		HashList<int> lzhl = [];
		List<SumSet<(uint, uint, uint, uint)>> sumSets = [];
		SumList lzLengthsSL = [1];
		uint lzCount = 1, notLZCount = 1, spaceCount = 1, notSpaceCount = 1;
		LimitedQueue<bool> spaceBuffer = new(maxDepth);
		LimitedQueue<uint> newItemsBuffer = new(maxDepth);
		var nextTarget = 0;
		for (var i = startPos; i < input.Length; i++, Status[tn]++)
		{
			var item = cout ? (input[i][0].Lower, 0, 0, 0) : (input[i][0].Lower, input[i][1].Lower, input[i][2].Lower, input[i][3].Lower);
			input.GetSlice(Max(startPos, i - maxDepth)..i).ForEach((x, index) => context.SetOrAdd(index, (x[0].Lower, x[1].Lower, x[2].Lower, x[3].Lower)));
			context.Reverse();
			context2.Replace(context);
			if (i < nextTarget)
				goto l1;
			List<Interval> intervalsForBuffer = [];
			if (context.Length == maxDepth && i >= (maxDepth << 1) + startPos && ProcessLZ(context, item, i) && i < nextTarget)
				goto l1;
			set.Clear();
			excludingSet.Clear();
			for (; context.Length > 0 && !contextHS.TryGetIndexOf(context, out _); context.RemoveAt(^1)) ;
			long sum = 0;
			var frequency = 0;
			for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out var index) && (sum = set.Replace(sumSets[index]).ExceptWith(excludingSet).GetLeftValuesSum(item, out frequency)) >= 0 && frequency == 0; context.RemoveAt(^1), excludingSet.UnionWith(set))
				if (set.Length != 0)
					intervalsForBuffer.Add(new((uint)set.ValuesSum, (uint)set.Length * 100, (uint)(set.ValuesSum + set.Length * 100)));
			if (set.Length == 0 || context.Length == 0)
			{
				excludingSet.ForEach(x => excludingSet.Update(x.Key, globalSet.TryGetValue(x.Key, out var newValue) ? newValue : throw new EncoderFallbackException()));
				set2 = globalSet.ExceptWith(excludingSet);
			}
			else
				set2 = set;
			if (frequency == 0)
				sum = set2.GetLeftValuesSum(item, out frequency);
			if (frequency == 0)
			{
				if (set2.Length != 0)
					intervalsForBuffer.Add(new((uint)set2.ValuesSum, (uint)set2.Length * 100, (uint)(set2.ValuesSum + set2.Length * 100)));
				if (n != 2)
				{
					intervalList.Add(new(item.Item1, inputBase));
					intervalList.Add(new(item.Item2, base2));
					intervalList.Add(new(item.Item3, base3));
					intervalList.Add(new(item.Item4, base4));
				}
			}
			else
			{
				intervalsForBuffer.Add(new(0, (uint)set2.ValuesSum, (uint)(set2.ValuesSum + set2.Length * 100)));
				intervalsForBuffer.Add(new((uint)sum, (uint)frequency, (uint)set2.ValuesSum));
				newItemsBuffer.Enqueue(uint.MaxValue);
			}
			if (set.Length == 0 || context.Length == 0)
				globalSet.UnionWith(excludingSet);
			var isSpace = false;
			if (n == 2)
			{
				isSpace = input[i][1].Lower != 0;
				uint bufferSpaces = (uint)spaceBuffer.Count(true), bufferNotSpaces = (uint)spaceBuffer.Count(false);
				intervalsForBuffer.Add(new(isSpace ? notSpaceCount + bufferNotSpaces : 0, isSpace ? spaceCount + bufferSpaces : notSpaceCount + bufferNotSpaces, notSpaceCount + spaceCount + (uint)spaceBuffer.Length));
			}
			else
				for (var j = cout ? 1 : 4; j < input[i].Length; j++)
					intervalsForBuffer.Add(new(input[i][j].Lower, input[i][j].Length, input[i][j].Base));
			if (buffer.IsFull)
				buffer.Dequeue().ForEach(x => intervalList.Add(x));
			buffer.Enqueue(intervalsForBuffer);
			if (n == 2 && spaceBuffer.IsFull)
			{
				var space2 = spaceBuffer.Dequeue();
				if (space2)
					spaceCount++;
				else
					notSpaceCount++;
			}
			spaceBuffer.Enqueue(isSpace);
		l1:
			var contextLength = context2.Length;
			Increase(context2, context, item, out var hlIndex);
			if (contextLength == maxDepth)
				lzhl.SetOrAdd((i - startPos - maxDepth) % LZDictionarySize, hlIndex);
		}
		while (buffer.Length != 0)
			buffer.Dequeue().ForEach(x => intervalList.Add(x));
		bool ProcessLZ(NList<(uint, uint, uint, uint)> context, (uint, uint, uint, uint) item, int curPos)
		{
			if (!buffer.IsFull)
				return false;
			var bestDist = -1;
			var bestLength = -1;
			var contextIndex = contextHS.IndexOf(context);
			foreach (var pos in lzhl.IndexesOf(contextIndex))
			{
				var dist = (pos - (curPos - startPos - maxDepth)) % LZDictionarySize + curPos - startPos - maxDepth;
				int length;
				for (length = -maxDepth; length < input.Length - startPos - curPos && RedStarLinq.Equals(input[curPos + length], input[dist + maxDepth + startPos + length], (x, y) => x.Lower == y.Lower); length++) ;
				if (curPos - (dist + maxDepth + startPos) >= 2 && length > bestLength)
				{
					bestDist = dist;
					bestLength = length;
				}
			}
			if (bestDist == -1)
			{
				if (buffer.IsFull)
				{
					intervalList.Add(new(0, notLZCount, lzCount + notLZCount));
					notLZCount++;
				}
				return false;
			}
			intervalList.Add(new(notLZCount, lzCount, lzCount + notLZCount));
			lzCount++;
			intervalList.Add(new((uint)(curPos - (bestDist + maxDepth + startPos) - 2), (uint)Min(curPos - startPos - maxDepth, LZDictionarySize - 1)));
			if (bestLength < lzLengthsSL.Length - 1)
			{
				intervalList.Add(new((uint)lzLengthsSL.GetLeftValuesSum(bestLength, out var frequency), (uint)frequency, (uint)lzLengthsSL.ValuesSum));
				lzLengthsSL.Increase(bestLength);
			}
			else
			{
				intervalList.Add(new((uint)(lzLengthsSL.ValuesSum - lzLengthsSL[^1]), (uint)lzLengthsSL[^1], (uint)lzLengthsSL.ValuesSum));
				lzLengthsSL.Increase(lzLengthsSL.Length - 1);
				var bits = EncodeFibonacci((uint)(bestLength - lzLengthsSL.Length + 2));
				for (var i = 0; i < bits.Length; i++)
					intervalList.Add(new((uint)(bits[i] ? 1 : 0), 2));
				new Chain(bestLength - lzLengthsSL.Length + 1).ForEach(x => lzLengthsSL.Insert(lzLengthsSL.Length - 1, 1));
			}
			buffer.Clear();
			spaceBuffer.Clear();
			//if (n != 2)
			//	newItemsBuffer.Filter(x => x != uint.MaxValue).ForEach(x => newItemsSet.Add((x, 1)));
			newItemsBuffer.Clear();
			nextTarget = curPos + bestLength;
			return true;
		}
		void Increase(NList<(uint, uint, uint, uint)> context, NList<(uint, uint, uint, uint)> successContext, (uint, uint, uint, uint) item, out int outIndex)
		{
			outIndex = -1;
			for (; context.Length > 0 && contextHS.TryAdd(context.Copy(), out var index); context.RemoveAt(^1))
			{
				if (outIndex == -1)
					outIndex = index;
				sumSets.SetOrAdd(index, [(item, 100)]);
			}
			var successLength = context.Length;
			_ = context.Length == 0 ? null : successContext.Replace(context).RemoveAt(^1);
			for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out var index); context.RemoveAt(^1), _ = context.Length == 0 ? null : successContext.RemoveAt(^1))
			{
				if (outIndex == -1)
					outIndex = index;
				if (!sumSets[index].TryGetValue(item, out var itemValue))
				{
					sumSets[index].Add(item, 100);
					continue;
				}
				else if (context.Length == 1 || itemValue > 100)
				{
					sumSets[index].Update(item, itemValue + (int)Max(Round((double)100 / (successLength - context.Length + 1)), 1));
					continue;
				}
				var successIndex = contextHS.IndexOf(successContext);
				if (!sumSets[successIndex].TryGetValue(item, out var successValue))
					successValue = 100;
				var step = (double)(sumSets[index].ValuesSum + sumSets[index].Length * 100) * successValue / (sumSets[index].ValuesSum + sumSets[successIndex].ValuesSum + sumSets[successIndex].Length * 100 - successValue);
				sumSets[index].Update(item, (int)(Max(Round(step), 1) + itemValue));
			}
			if (globalSet.TryGetValue(item, out var globalValue))
				globalSet.Update(item, globalValue + (int)Max(Round((double)100 / (successLength + 1)), 1));
			else
				globalSet.Add(item, 100);
		}
		var result = intervalList.SplitIntoEqual(8).PConvert(x => new ShortIntervalList(x));
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

	private static void Encode1(List<List<Bgra32>> input, int n, ref byte[] cs, int tn)
	{
		const int methodsCount = 9;
		var ql = n < 4 ? input.ToTripleList() : input.ToQuadrupleList();
		var sum = ql.ToList(tl => tl.Sum(dl => dl.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length)))) / Log(256));
		var ctl = RedStarLinq.Fill(ql.Length, _ => new List<ShortIntervalList>());
		bool hf = PresentMethodsI.HasFlag(tn switch { 0 => UsedMethodsI.HF1, 1 => UsedMethodsI.HF2, 2 => UsedMethodsI.CS3, 3 => UsedMethodsI.CS4, _ => throw new EncoderFallbackException() }), bwt = n >= 2, lz = PresentMethodsI.HasFlag(tn switch { 0 => UsedMethodsI.LZ1, 1 => UsedMethodsI.LZ2, 2 => UsedMethodsI.LZ3, 3 => UsedMethodsI.LZ4, _ => throw new EncoderFallbackException() }) && !bwt;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * methodsCount * ql.Length * 3;
		for (var tlIndex = 0; tlIndex < ql.Length; tlIndex++)
		{
			for (var i = 0; i < methodsCount; i++)
			{
				Current[tn] = 0;
				CurrentMaximum[tn] = ProgressBarStep * 6;
				GC.Collect();
				var tl1 = ql[tlIndex].ToList(dl => dl.ToList(l => new ShortIntervalList(l)));
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
				if (s1 > 0 && (s1 < sum[tlIndex] || sum[tlIndex] == 0))
				{
					ctl[tlIndex] = dl1;
					sum[tlIndex] = s1;
				}
				Subtotal[tn] += ProgressBarStep;
			}
		}
		cs = WorkUpDoubleList(ctl.JoinIntoSingle(), tn);
	}

	private static void LWEncode(List<List<Bgra32>> input, ref byte[] cs, ref int lw, int tn)
	{
		List<List<ShortIntervalList>> ctl;
		var bufferLower = RedStarLinq.Fill((uint)ValuesInByte, input.Sum(x => x.Length));
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 40;
		ctl = ToTripleList(input);
		Subtotal[tn] += ProgressBarStep;
		cs = WorkUpDoubleList(ctl.ConvertAndJoin(dl => dl).ToList(), tn);
		Subtotal[tn] += ProgressBarStep;
		LosslessWavelet(ctl, ref cs, ref lw, tn);
	}

	public static byte[] Encode(Image<Bgra32> originalImage, byte[] originalBytes)
	{
		var s = RedStarLinq.FillArray(ProgressBarGroups, _ => originalBytes);
		var cs = originalBytes;
		int lw = 0, part = 0, lwP5 = 0;
		List<List<Bgra32>> input = [];
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
					Encode1(input, 0, ref s[0], 0);
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
					Encode1(input, 1, ref s[1], 1);
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
					Encode1(input, 2, ref s[2], 2);
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
					Encode1(input, 3, ref s[3], 3);
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
				if ((PresentMethodsI & UsedMethodsI.CS6) != 0)
					LWEncode(input, ref s[4], ref lwP5, 4);
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
		//if ((PresentMethods & UsedMethods.CS7) != 0 && s[6].Length < cs.Length && s[6].Length > 0 && s.AsSpan(0, 6).All(x => s[6].Length < x.Length))
		//{
		//	cs = s[6];
		//}
		//else if ((PresentMethods & UsedMethods.CS6) != 0 && s[5].Length < cs.Length && s[5].Length > 0 && s.AsSpan(0, 5).All(x => s[5].Length < x.Length))
		//{
		//	cs = s[5];
		//}
		if ((PresentMethodsI & UsedMethodsI.CS6) != 0 && s[4].Length < cs.Length && s[4].Length > 0 && s.AsSpan(0, 4).All(x => s[4].Length < x.Length))
		{
			lw = lwP5;
			part = 8;
			cs = s[4];
		}
		else if ((PresentMethodsI & UsedMethodsI.CS4) != 0 && s[3].Length < cs.Length && s[3].Length > 0 && s.AsSpan(0, 3).All(x => s[3].Length < x.Length))
		{
			part = 0;
			cs = s[3];
		}
		else if ((PresentMethodsI & UsedMethodsI.CS3) != 0 && s[2].Length < cs.Length && s[2].Length > 0 && s[2].Length < s[1].Length && s[2].Length < s[0].Length)
		{
			part = 0;
			cs = s[2];
		}
		else if ((PresentMethodsI & UsedMethodsI.CS2) != 0 && (s[1].Length < cs.Length || s[1].Length > 0 && cs.Length == 0) && s[1].Length < s[0].Length)
		{
			part = 0;
			cs = s[1];
		}
		else if ((PresentMethodsI & UsedMethodsI.CS1) != 0 && s[0].Length < cs.Length || s[0].Length > 0 && cs.Length == 0)
		{
			part = 0;
			cs = s[0];
		}
		else
			return [0, .. originalBytes];
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
}
