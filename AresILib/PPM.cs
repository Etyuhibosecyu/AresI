namespace AresILib;

internal record class PPM(bool Cout, int TN)
{
	private ArithmeticEncoder ar = default!;
	private readonly List<List<Interval>> result = [];

	public List<ShortIntervalList> Encode(List<ShortIntervalList> input)
	{
		if (input.Length < 4)
			throw new EncoderFallbackException();
		ar = new();
		result.Replace(new List<List<Interval>>(new List<Interval>()));
		if (!new PPMInternal(input, result[0], 1, Cout, true, TN).Encode())
			throw new EncoderFallbackException();
		result[0].ForEach(x => ar.WritePart(x.Lower, x.Length, x.Base));
		ar.WriteEqual(1234567890, 4294967295);
		var result2 = result[0].SplitIntoEqual(8).PConvert(x => new ShortIntervalList(x));
		return result2.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length))) < input.Sum(l => l.Sum(x => Log(x.Base) - Log(x.Length))) ? input.Replace(result2) : input;
	}
}

file record class PPMInternal(List<ShortIntervalList> Input, List<Interval> Result, int N, bool Cout, bool LastDoubleList, int TN)
{
	private const int LZDictionarySize = 8388607;
	private int startPos = 1;
	private readonly SumSet<(uint, uint, uint, uint)> globalSet = [];
	private const int maxDepth = 12;
	private readonly LimitedQueue<List<Interval>> buffer = new(maxDepth);
	private G.IEqualityComparer<NList<(uint, uint, uint, uint)>> comparer = default!;
	private FastDelHashSet<NList<(uint, uint, uint, uint)>> contextHS = default!;
	private HashList<int> lzhl = default!;
	private readonly List<SumSet<(uint, uint, uint, uint)>> sumSets = [];
	private readonly SumList lzLengthsSL = [];
	private uint lzCount, notLZCount, spaceCount, notSpaceCount;
	private readonly LimitedQueue<bool> spaceBuffer = new(maxDepth);
	private readonly NList<(uint, uint, uint, uint)> context = new(maxDepth), context2 = new(maxDepth);
	private readonly SumSet<(uint, uint, uint, uint)> set = [], excludingSet = [];
	private readonly List<Interval> intervalsForBuffer = [];
	private int nextTarget = 0;

	public bool Encode()
	{
		if (!(Input.Length >= 4 && Input[CreateVar(Input[0].Length >= 1 && Input[0][0] == LengthsApplied ? (int)Input[0][1].Base + 1 : 3, out startPos)].Length is 4 or 5 or 7 && Input[startPos].All(x => x.Length == 1) && Input.GetSlice(startPos + 1).All(x => x.Length is 4 or 5 or 7 && x.All(y => y.Length == 1) && x.GetSlice(..4).All((y, index) => y.Base == Input[startPos][index].Base) && (x.Length == 4 || x[4].Length == 1 && x[4].Base == Input[startPos][4].Base))))
			throw new EncoderFallbackException();
		if (LastDoubleList)
		{
			Status[TN] = 0;
			StatusMaximum[TN] = Input.Length - startPos;
		}
		for (var i = 0; i < Input[0].Length; i++)
			Result.Add(new(Input[0][i].Lower, Input[0][i].Length, Input[0][i].Base));
		var (inputBase, base2, base3, base4) = Cout ? (1, 1, 1, 1) : (Input[startPos][0].Base, Input[startPos][1].Base, Input[startPos][2].Base, Input[startPos][3].Base);
		Result.WriteCount((uint)(Input.Length - startPos));
		Result.WriteCount(LZDictionarySize);
		PrepareFields(inputBase, base2, base3, base4);
		for (var i = startPos; i < Input.Length; i++, _ = LastDoubleList ? Status[TN]++ : 0)
		{
			var item = (Input[i][0].Lower, Input[i][1].Lower, Input[i][2].Lower, Input[i][3].Lower);
			Input.GetSlice(Max(startPos, i - maxDepth)..i).ForEach((x, index) => context.SetOrAdd(index, (x[0].Lower, x[1].Lower, x[2].Lower, x[3].Lower)));
			context.Reverse();
			context2.Replace(context);
			if (i < nextTarget)
				goto l1;
			intervalsForBuffer.Clear();
			if (context.Length == maxDepth && i >= (maxDepth << 1) + startPos && ProcessLZ(context, i) && i < nextTarget)
				goto l1;
			set.Clear();
			excludingSet.Clear();
			Escape(item, out var sum, out var frequency);
			ProcessFrequency(item, inputBase, base2, base3, base4, ref sum, ref frequency);
			ProcessBuffers(i);
		l1:
			var contextLength = context2.Length;
			Increase(context2, context, item, out var hlIndex);
			if (contextLength == maxDepth)
				lzhl.SetOrAdd((i - startPos - maxDepth) % LZDictionarySize, hlIndex);
		}
		while (buffer.Length != 0)
			buffer.Dequeue().ForEach(x => Result.Add(new(x.Lower, x.Length, x.Base)));
		return true;
	}

	private void PrepareFields(uint inputBase, uint base2, uint base3, uint base4)
	{
		globalSet.Clear();
		buffer.Clear();
		comparer = N == 2 ? new NListEComparer<(uint, uint, uint, uint)>() : new EComparer<NList<(uint, uint, uint, uint)>>((x, y) => x.Equals(y), x => unchecked(x.Progression(17 * 23 + x.Length, (x, y) => x * 23 + y.GetHashCode())));
		contextHS = new(comparer);
		lzhl = [];
		sumSets.Clear();
		lzLengthsSL.Replace(new[] { 1 });
		lzCount = notLZCount = spaceCount = notSpaceCount = 1;
		spaceBuffer.Clear();
		context.Clear();
		context2.Clear();
		set.Clear();
		excludingSet.Clear();
		intervalsForBuffer.Clear();
		nextTarget = 0;
	}

	private void Escape((uint, uint, uint, uint) item, out long sum, out int frequency)
	{
		for (; context.Length > 0 && !contextHS.TryGetIndexOf(context, out _); context.RemoveAt(^1)) ;
		sum = 0;
		frequency = 0;
		for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out var index) && (sum = set.Replace(sumSets[index]).ExceptWith(excludingSet).GetLeftValuesSum(item, out frequency)) >= 0 && frequency == 0; context.RemoveAt(^1), excludingSet.UnionWith(set))
			if (set.Length != 0)
				intervalsForBuffer.Add(new((uint)set.ValuesSum, (uint)set.Length * 100, (uint)(set.ValuesSum + set.Length * 100)));
		if (set.Length == 0 || context.Length == 0)
			set.Replace(globalSet).ExceptWith(excludingSet);
	}

	private void ProcessFrequency((uint, uint, uint, uint) item, uint inputBase, uint base2, uint base3, uint base4, ref long sum, ref int frequency)
	{
		if (frequency == 0)
			sum = set.GetLeftValuesSum(item, out frequency);
		if (frequency == 0)
		{
			if (set.Length != 0)
				intervalsForBuffer.Add(new((uint)set.ValuesSum, (uint)set.Length * 100, (uint)(set.ValuesSum + set.Length * 100)));
			if (N != 2)
			{
				intervalsForBuffer.Add(new(item.Item1, inputBase));
				intervalsForBuffer.Add(new(item.Item2, base2));
				intervalsForBuffer.Add(new(item.Item3, base3));
				intervalsForBuffer.Add(new(item.Item4, base4));
			}
		}
		else
		{
			intervalsForBuffer.Add(new(0, (uint)set.ValuesSum, (uint)(set.ValuesSum + set.Length * 100)));
			intervalsForBuffer.Add(new((uint)sum, (uint)frequency, (uint)set.ValuesSum));
		}
	}

	private void ProcessBuffers(int i)
	{
		var isSpace = false;
		if (N == 2)
		{
			isSpace = Input[i][1].Lower != 0;
			uint bufferSpaces = (uint)spaceBuffer.Count(true), bufferNotSpaces = (uint)spaceBuffer.Count(false);
			intervalsForBuffer.Add(new(isSpace ? notSpaceCount + bufferNotSpaces : 0, isSpace ? spaceCount + bufferSpaces : notSpaceCount + bufferNotSpaces, notSpaceCount + spaceCount + (uint)spaceBuffer.Length));
		}
		else
			for (var j = 1; j < Input[i].Length; j++)
				intervalsForBuffer.Add(new(Input[i][j].Lower, Input[i][j].Length, Input[i][j].Base));
		if (buffer.IsFull)
			buffer.Dequeue().ForEach(x => Result.Add(new(x.Lower, x.Length, x.Base)));
		buffer.Enqueue(intervalsForBuffer.Copy());
		if (N == 2 && spaceBuffer.IsFull)
		{
			var space2 = spaceBuffer.Dequeue();
			if (space2)
				spaceCount++;
			else
				notSpaceCount++;
		}
		spaceBuffer.Enqueue(isSpace);
	}

	bool ProcessLZ(NList<(uint, uint, uint, uint)> context, int curPos)
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
			for (length = -maxDepth; length < Input.Length - startPos - curPos && RedStarLinq.Equals(Input[curPos + length], Input[dist + maxDepth + startPos + length], (x, y) => x.Lower == y.Lower); length++) ;
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
				Result.Add(new(0, notLZCount, lzCount + notLZCount));
				notLZCount++;
			}
			return false;
		}
		Result.Add(new(notLZCount, lzCount, lzCount + notLZCount));
		lzCount++;
		Result.Add(new((uint)(curPos - (bestDist + maxDepth + startPos) - 2), (uint)Min(curPos - startPos - maxDepth, LZDictionarySize - 1)));
		if (bestLength < lzLengthsSL.Length - 1)
		{
			Result.Add(new((uint)lzLengthsSL.GetLeftValuesSum(bestLength, out var frequency), (uint)frequency, (uint)lzLengthsSL.ValuesSum));
			lzLengthsSL.Increase(bestLength);
		}
		else
		{
			Result.Add(new((uint)(lzLengthsSL.ValuesSum - lzLengthsSL[^1]), (uint)lzLengthsSL[^1], (uint)lzLengthsSL.ValuesSum));
			lzLengthsSL.Increase(lzLengthsSL.Length - 1);
			foreach (var bit in EncodeFibonacci((uint)(bestLength - lzLengthsSL.Length + 2)))
				Result.Add(new(bit ? 1u : 0, 2));
			new Chain(bestLength - lzLengthsSL.Length + 1).ForEach(x => lzLengthsSL.Insert(lzLengthsSL.Length - 1, 1));
		}
		buffer.Clear();
		spaceBuffer.Clear();
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
}
