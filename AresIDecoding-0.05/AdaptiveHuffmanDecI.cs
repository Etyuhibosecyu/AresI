
namespace AresILib;

public class AdaptiveHuffmanDecI : IDisposable
{
	protected GlobalDecoding decoding = default!;
	protected ArithmeticDecoder ar = default!;
	protected NList<ShortIntervalList> result = default!;
	protected NList<byte> skipped = default!;
	protected SumSet<(uint, uint, uint, uint)> set = default!, newItems = default!;
	protected NList<(Interval, Interval, Interval, Interval)> uniqueList = default!;
	protected LZData lzData = default!;
	protected uint fileBase, base2, base3, base4, nextWordLink;
	protected int rle, lz, bwt, delta, fullLength, bwtBlockSize, bwtBlockExtraSize, counter;
	protected SumList lengthsSL, distsSL;
	protected NList<int> values = default!;
	protected int leftSerie, deltaSum, lzLength;
	protected uint firstIntervalDist;

	public AdaptiveHuffmanDecI(GlobalDecoding decoding, ArithmeticDecoder ar, LZData lzData, int rle, int lz, int bwt, int delta, NList<byte> skipped, int counter)
	{
		this.decoding = decoding;
		this.ar = ar;
		this.lzData = lzData;
		this.rle = rle;
		this.lz = lz;
		this.bwt = bwt;
		this.delta = delta;
		this.skipped = skipped;
		this.counter = counter;
		Prerequisites(ar, bwt, counter);
		if (lz != 0)
		{
			(lengthsSL = []).AddSeries(1, (int)(lzData.Length.R == 0 ? lzData.Length.Max + 1 : lzData.Length.R == 1 ? lzData.Length.Threshold + 2 : lzData.Length.Max - lzData.Length.Threshold + 2));
			(distsSL = []).AddSeries(1, (int)lzData.UseSpiralLengths + 1);
			firstIntervalDist = (lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max + 1) + lzData.UseSpiralLengths;
		}
		else
		{
			lengthsSL = [];
			distsSL = [];
			firstIntervalDist = 0;
		}
	}

	public virtual void Dispose()
	{
		set?.Dispose();
		uniqueList?.Dispose();
		lengthsSL?.Dispose();
		distsSL?.Dispose();
		values?.Dispose();
		GC.SuppressFinalize(this);
	}

	public NList<ShortIntervalList> Decode()
	{
		Prerequisites2();
		while (counter > 0)
			DecodeIteration();
		Current[0] += ProgressBarStep;
		return Postrequisites();
	}

	protected virtual void Prerequisites(ArithmeticDecoder ar, int bwt, int counter)
	{
		if (bwt != 0)
			DecodeSkipped();
		if (bwt != 0)
			this.counter = (int)ar.ReadCount();
		fileBase = ar.ReadCount();
		base2 = ar.ReadCount();
		base3 = ar.ReadCount();
		base4 = ar.ReadCount();
		if (counter is < 0)
			throw new DecoderFallbackException();
		Status[0] = 0;
		StatusMaximum[0] = counter;
		set = [((uint.MaxValue, 0, 0, 0), 1)];
	}

	protected virtual void DecodeSkipped()
	{
		var skippedCount = (int)ar.ReadCount();
		for (var i = 0; i < skippedCount; i++)
			skipped.Add((byte)ar.ReadEqual(ValuesInByte));
		counter -= (skippedCount + 9) / 8;
	}

	protected virtual void Prerequisites2()
	{
		leftSerie = 0;
		values = [];
		uniqueList = [];
		if (lz != 0)
		{
			set.Add(((fileBase - 1, 0, 0, 0), 1));
			uniqueList.Add((new(fileBase - 1, fileBase), new(), new(), new()));
		}
		result = [];
		deltaSum = 0;
		lzLength = 0;
	}

	protected virtual void DecodeIteration()
	{
		var readItem = ReadFirst();
		if (bwt != 0)
		{
			result.Add([uniqueList[readItem].Item1]);
			counter--;
			return;
		}
		if (!(lz != 0 && uniqueList[readItem].Item1.Lower == fileBase - 1))
		{
			DecodeWithoutLZ(readItem);
			return;
		}
		decoding.ProcessLZLength(lzData, lengthsSL, out readItem, out var length);
		decoding.ProcessLZDist(lzData, distsSL, result.Length, out readItem, out var dist, length, out var maxDist);
		decoding.ProcessLZSpiralLength(lzData, ref dist, out var spiralLength, maxDist);
		var start = (int)(result.Length - dist - length - 2);
		if (start < 0)
			throw new DecoderFallbackException();
		var fullLength = (int)((length + 2) * (spiralLength + 1));
		RewindLZBlock(length, start, fullLength);
		UpdateLeftSerie(fullLength);
		lzLength++;
		if (lz != 0 && distsSL.Length < firstIntervalDist)
			new Chain((int)Min(firstIntervalDist - distsSL.Length, (length + 2) * (spiralLength + 1))).ForEach(x => distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1));
	}

	protected virtual int ReadFirst()
	{
		var readItem = ar.ReadPart(set);
		if (readItem == set.Length - 1)
			readItem = ReadNewItem();
		else
		{
			var Item1 = uniqueList[readItem].Item1.Lower;
			set.Increase((Item1, uniqueList[readItem].Item2.Lower, uniqueList[readItem].Item3.Lower, uniqueList[readItem].Item4.Lower));
			deltaSum = delta == 0 || result.Length == 0 ? (byte)Item1 : unchecked((byte)(deltaSum + Item1 + (ValuesInByte >> 1)));
		}
		FirstUpdateSet();
		return readItem;
	}

	protected virtual int ReadNewItem()
	{
		var Item1 = ar.ReadEqual(fileBase);
		deltaSum = delta == 0 || result.Length == 0 ? (byte)Item1 : unchecked((byte)(deltaSum + Item1 + (ValuesInByte >> 1)));
		var newItem = (Item1, ar.ReadEqual(base2), ar.ReadEqual(base3), ar.ReadEqual(base4));
		if (!set.TryAdd((newItem, 1), out var readItem))
			throw new DecoderFallbackException();
		uniqueList.Insert(readItem, (new Interval(newItem.Item1, fileBase - (lz != 0 ? 1u : 0)), new(newItem.Item2, base2), new(newItem.Item3, base3), new(newItem.Item4, base4)));
		return readItem;
	}

	protected virtual void FirstUpdateSet() => set.Update((uint.MaxValue, 0, 0, 0), Max(set.Length - 1, 1));

	protected virtual void DecodeWithoutLZ(int readItem)
	{
		ShortIntervalList list = [uniqueList[readItem].Item1, uniqueList[readItem].Item2, uniqueList[readItem].Item3, uniqueList[readItem].Item4];
		if (rle == 0 || leftSerie > 0)
		{
			leftSerie--;
			counter--;
			Status[0]++;
			values.Add(1);
		}
		else
			ReadRLEWithoutLZ(list);
		result.Add(list);
		lzLength++;
		if (lz != 0 && distsSL.Length < firstIntervalDist)
			distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1);
	}

	protected virtual void ReadRLEWithoutLZ(ShortIntervalList list)
	{
		var newSerie = ar.ReadEqual(ValuesInByte);
		list.Add(new(newSerie, ValuesInByte));
		int value;
		if (newSerie % (ValuesInByte >> 1) == (ValuesInByte >> 1) - 1)
		{
			var newSerieQ = ar.ReadEqual(ValuesInByte);
			list.Add(new(newSerieQ, ValuesInByte));
			var newSerieR = ar.ReadEqual(ValuesInByte);
			list.Add(new(newSerieR, ValuesInByte));
			value = (int)((newSerieQ << BitsPerByte) + newSerieR + (ValuesInByte >> 1));
		}
		else
			value = (int)(newSerie % (ValuesInByte >> 1) + 1);
		UpdateLeftSerieWithoutLZ(newSerie, value);
	}

	protected virtual void UpdateLeftSerieWithoutLZ(uint newSerie, int value)
	{
		if (newSerie >= ValuesInByte >> 1)
		{
			leftSerie = value - 2;
			counter--;
			Status[0]++;
			values.Add(1);
		}
		else
		{
			counter -= value;
			Status[0] += value;
			values.Add(value);
		}
		if (counter < 0)
			throw new DecoderFallbackException();
	}

	protected virtual void RewindLZBlock(uint length, int start, int fullLength)
	{
		for (var i = fullLength; i > 0; i -= (int)length + 2)
		{
			var length2 = (int)Min(length + 2, i);
			result.AddRange(result.GetRange(start, length2));
			var valuesRange = values.GetRange(start, length2);
			values.AddRange(valuesRange);
			var decrease = valuesRange.Sum();
			counter -= decrease;
			if (counter < 0)
				throw new DecoderFallbackException();
			Status[0] += decrease;
		}
	}

	protected virtual void UpdateLeftSerie(int fullLength)
	{
		if (leftSerie >= fullLength)
			leftSerie -= fullLength;
		else if (rle != 0)
			ReadRLEWithLZ(fullLength);
	}

	protected virtual void ReadRLEWithLZ(int fullLength)
	{
		const int mainCount = 4;
		var findIndex = result.FindLastIndex(result.Length - 1, fullLength, x => x.Length > mainCount);
		if (findIndex == -1)
			throw new DecoderFallbackException();
		var newSerie = result[findIndex][mainCount].Lower;
		int value;
		if (newSerie % (ValuesInByte >> 1) == (ValuesInByte >> 1) - 1)
		{
			var newSerieQ = result[findIndex][mainCount + 1].Lower;
			var newSerieR = result[findIndex][mainCount + 2].Lower;
			value = (int)((newSerieQ << BitsPerByte) + newSerieR + (ValuesInByte >> 1));
		}
		else
			value = (int)(newSerie % (ValuesInByte >> 1) + 1);
		UpdateLeftSerieWithLZ(findIndex, newSerie, value);
	}

	private void UpdateLeftSerieWithLZ(int findIndex, uint newSerie, int value)
	{
		if (newSerie >= ValuesInByte >> 1)
			leftSerie = value - 2 - (result.Length - findIndex - 1);
		else if (result.Length == findIndex + 1)
			leftSerie = 0;
		else
			throw new DecoderFallbackException();
		if (newSerie < ValuesInByte >> 1 || delta == 0)
			deltaSum += unchecked((byte)((value - 1) * (result[findIndex][0].Lower + (ValuesInByte >> 1))));
	}

	protected virtual NList<ShortIntervalList> Postrequisites() => result;
}