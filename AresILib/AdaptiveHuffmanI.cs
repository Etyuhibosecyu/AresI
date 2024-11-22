
namespace AresILib;

internal record class AdaptiveHuffmanI(int TN)
{
	private bool lz;
	private int bwtLength, startPos;
	private uint firstIntervalDist;
	private readonly SumSet<(uint, uint, uint, uint)> set = [];
	private readonly SumList lengthsSL = [], distsSL = [];

	public NList<ShortIntervalList> Encode(NList<ShortIntervalList> input, LZData lzData, bool cout = false)
	{
		if (input.Length < 2)
			throw new EncoderFallbackException();
		using NList<Interval> intervalList = [];
		if (!AdaptiveHuffmanInternal(intervalList, input, lzData, cout))
			return input;
		var result = intervalList.SplitIntoEqual(8).PConvert(x => new ShortIntervalList(x));
		return result.Sum(l => Log(l.Product(x => (double)x.Base / x.Length))) < input.Sum(l => Log(l.Product(x => (double)x.Base / x.Length))) ? input.Replace(result) : input;
	}

	private bool AdaptiveHuffmanInternal(NList<Interval> intervalList, NList<ShortIntervalList> input, LZData lzData, bool cout = false)
	{
		int bwtIndex;
		try
		{
			Prerequisites(input, out bwtIndex, cout);
		}
		catch
		{
			return false;
		}
		for (var i = 0; i < startPos; i++)
			for (var j = 0; j < input[i].Length; j++)
			{
				var x = input[i][j];
				intervalList.Add(i == 1 && j == input[i].Length - 1 ? new(x.Lower / 8 * 8 + x.Lower % 2 + 4, 24) : x);
			}
		Status[TN]++;
		if (bwtLength != 0)
			intervalList.WriteCount((uint)(input.Length - startPos));
		var newBase = input[startPos][0].Base + (lz ? 1u : 0);
		uint GetBase(int index) => input.GetRange(startPos).Find(x => x[index].Base > 1)[index].Base;
		var (base2, base3, base4) = cout || bwtIndex != -1 ? (1, 1, 1) : (GetBase(1), GetBase(2), GetBase(3));
		intervalList.WriteCount(newBase);
		intervalList.WriteCount(base2);
		intervalList.WriteCount(base3);
		intervalList.WriteCount(base4);
		Status[TN] = 0;
		StatusMaximum[TN] = input.Length - startPos;
		Current[TN] += ProgressBarStep;
		set.Clear();
		if (lz)
		{
			using var streamOfUnits = RedStarLinq.NFill(1, (int)(lzData.Length.R == 0 ? lzData.Length.Max + 1 : lzData.Length.R == 1 ? lzData.Length.Threshold + 2 : lzData.Length.Max - lzData.Length.Threshold + 2));
			lengthsSL.Replace(streamOfUnits);
			streamOfUnits.Remove((int)lzData.UseSpiralLengths + 1);
			distsSL.Replace(streamOfUnits);
			firstIntervalDist = (lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max + 1) + lzData.UseSpiralLengths;
		}
		else
		{
			lengthsSL.Clear();
			distsSL.Clear();
			firstIntervalDist = 0;
		}
		if (lz)
			set.Add(((newBase - 1, 0, 0, 0), 1));
		new Encoder(intervalList, input, lzData, startPos, cout, bwtLength != 0, lz, newBase, base2, base3, base4, set, lengthsSL, distsSL, firstIntervalDist, TN).MainProcess();
		return true;
	}

	private void Prerequisites(NList<ShortIntervalList> input, out int bwtIndex, bool cout = false)
	{
		var bwtIndex2 = bwtIndex = input[0].IndexOf(BWTApplied);
		if (CreateVar(input[0].IndexOf(HuffmanApplied), out var huffmanIndex) != -1 && !(bwtIndex != -1 && huffmanIndex == bwtIndex + 1))
			throw new EncoderFallbackException();
		Current[TN] = 0;
		CurrentMaximum[TN] = ProgressBarStep * 2;
		Status[TN] = 0;
		StatusMaximum[TN] = 3;
		lz = CreateVar(input[0].IndexOf(LempelZivApplied), out var lzIndex) != -1 && (bwtIndex == -1 || lzIndex != bwtIndex + 1);
		var lzDummy = CreateVar(input[0].IndexOf(LempelZivDummyApplied), out var lzDummyIndex) != -1 && (bwtIndex == -1 || lzDummyIndex != bwtIndex + 1);
		bwtLength = bwtIndex != -1 ? (int)input[0][bwtIndex + 1].Base : 0;
		startPos = (lz || lzDummy ? (input[0].Length >= lzIndex + 2 && input[0][lzIndex + 1] == LempelZivSubdivided ? 4 : 3) : 2) + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? 1 : 0) + (cout ? 0 : 1) + bwtLength;
		Status[TN]++;
		var lzPos = bwtIndex != -1 ? 4 : 2;
		if (input.Length < startPos + lzPos + 1)
			throw new EncoderFallbackException();
		var originalBase = input[startPos + lzPos][0].Base;
		if (!input.GetRange(startPos + lzPos + 1).All((x, index) => bwtIndex2 != -1 && (index + lzPos + 1) % (BWTBlockSize + 2) is 0 or 1 || x[0].Base == originalBase))
			throw new EncoderFallbackException();
		Status[TN]++;
	}
}

file sealed record class Encoder(NList<Interval> IntervalList, NList<ShortIntervalList> Input, LZData LZData,
	int StartPos, bool Cout, bool BWT, bool LZ, uint NewBase, uint Base2, uint Base3, uint Base4,
	SumSet<(uint, uint, uint, uint)> Set, SumList LengthsSL, SumList DistsSL, uint FirstIntervalDist, int TN)
{
	private int frequency, fullLength;
	private uint lzLength, lzDist, lzSpiralLength, maxDist, bufferInterval;
	private long sum;
	private (uint, uint, uint, uint) item;
	private uint lower;

	public void MainProcess()
	{
		fullLength = StartPos - 3;
		for (var i = StartPos; i < Input.Length; i++, Status[TN]++)
		{
			if (Cout || BWT || LZ && Input[i][0].Lower == NewBase - 1)
				item = (Input[i][0].Lower, 0, 0, 0);
			else
				item = (Input[i][0].Lower, Input[i][1].Lower, Input[i][2].Lower, Input[i][3].Lower);
			sum = Set.GetLeftValuesSum(item, out frequency);
			bufferInterval = Max((uint)Set.Length, 1);
			var fullBase = (uint)(Set.ValuesSum + bufferInterval);
			if (frequency == 0)
			{
				IntervalList.Add(new((uint)Set.ValuesSum, bufferInterval, fullBase));
				IntervalList.Add(new(item.Item1, NewBase));
				IntervalList.Add(new(item.Item2, Base2));
				IntervalList.Add(new(item.Item3, Base3));
				IntervalList.Add(new(item.Item4, Base4));
			}
			else
				IntervalList.Add(new((uint)sum, (uint)frequency, fullBase));
			Set.Increase(item);
			lzLength = lzDist = lzSpiralLength = 0;
			EncodeNextIntervals(i);
		}
	}

	private void EncodeNextIntervals(int inputIndex)
	{
		var innerIndex = Cout || BWT || LZ && item.Item1 == NewBase - 1 ? 1 : 4;
		if (LZ && item.Item1 == NewBase - 1)
		{
			EncodeLength(inputIndex, ref innerIndex);
			maxDist = Min(LZData.Dist.Max, (uint)(fullLength - lzLength - StartPos + 1));
			EncodeDist(inputIndex, ref innerIndex);
			EncodeSpiralLength(inputIndex, innerIndex);
			var fullLengthDelta = (lzLength + 2) * (lzSpiralLength + 1);
			fullLength += (int)fullLengthDelta;
			if (DistsSL.Length < FirstIntervalDist)
			{
				var insertionsCount = (int)Min(FirstIntervalDist - DistsSL.Length, fullLengthDelta);
				for (var i = 0; i < insertionsCount; i++)
					DistsSL.Insert(DistsSL.Length - ((int)LZData.UseSpiralLengths + 1), 1);
			}
		}
		else if (LZ)
		{
			fullLength++;
			if (DistsSL.Length < FirstIntervalDist)
				DistsSL.Insert(DistsSL.Length - ((int)LZData.UseSpiralLengths + 1), 1);
		}
		for (; innerIndex < Input[inputIndex].Length; innerIndex++)
			IntervalList.Add(Input[inputIndex][innerIndex]);
	}

	private void EncodeLength(int inputIndex, ref int innerIndex)
	{
		lower = Input[inputIndex][innerIndex].Lower;
		lzLength = lower + (LZData.Length.R == 2 ? LZData.Length.Threshold : 0);
		sum = LengthsSL.GetLeftValuesSum((int)lower, out frequency);
		IntervalList.Add(new((uint)sum, (uint)frequency, (uint)LengthsSL.ValuesSum));
		LengthsSL.Increase((int)lower);
		innerIndex++;
		if (LZData.Length.R != 0 && lower == LengthsSL.Length - 1)
		{
			IntervalList.Add(Input[inputIndex][innerIndex]);
			lzLength = Input[inputIndex][innerIndex].Lower + (LZData.Length.R == 2 ? 0 : LZData.Length.Threshold + 1);
			innerIndex++;
		}
	}

	private void EncodeDist(int inputIndex, ref int innerIndex)
	{
		lower = Input[inputIndex][innerIndex].Lower;
		sum = DistsSL.GetLeftValuesSum((int)lower, out frequency);
		IntervalList.Add(new((uint)sum, (uint)frequency, (uint)DistsSL.ValuesSum));
		DistsSL.Increase((int)lower);
		var addThreshold = maxDist >= LZData.Dist.Threshold;
		lzDist = lower + (LZData.Dist.R == 2 && addThreshold ? LZData.Dist.Threshold : 0);
		if (LZData.Dist.R == 1 && addThreshold && lzDist == LZData.Dist.Threshold + 1)
		{
			innerIndex++;
			lzDist = LZData.Dist.Threshold + Input[inputIndex][innerIndex].Lower + 1;
			IntervalList.Add(Input[inputIndex][innerIndex]);
		}
		if (LZData.Dist.R == 2 && addThreshold && lzDist == maxDist + 1)
		{
			innerIndex++;
			if (Input[inputIndex][innerIndex].Lower != LZData.Dist.Threshold)
				lzDist = Input[inputIndex][innerIndex].Lower;
		}
		innerIndex++;
	}

	private void EncodeSpiralLength(int inputIndex, int innerIndex)
	{
		if (LZData.UseSpiralLengths != 0 && Input[inputIndex][innerIndex - 1].Lower == Input[inputIndex][innerIndex - 1].Base - 1)
		{
			lzSpiralLength = Input[inputIndex][^1].Lower;
			if (LZData.SpiralLength.R == 1 != (innerIndex == Input[inputIndex].Length - 1))
				lzSpiralLength += LZData.SpiralLength.Threshold + 2 - LZData.SpiralLength.R;
		}
		else
			lzSpiralLength = 0;
	}
}
