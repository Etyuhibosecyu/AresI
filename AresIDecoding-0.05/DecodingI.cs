global using AresGlobalMethods;
global using Corlib.NStar;
global using Mpir.NET;
global using SixLabors.ImageSharp;
global using SixLabors.ImageSharp.PixelFormats;
global using System;
global using System.IO;
global using System.Net.Http;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static AresILib.Global;
global using static Corlib.NStar.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;
global using String = Corlib.NStar.String;

namespace AresILib;

public enum TraversalMode
{
	Table,
	TableV,
	Diagonal,
	Diagonal2,
	Spiral,
}

public enum UsedMethodsI
{
	None = 0,
	CS1 = 1,
	LZ1 = 1 << 1,
	HF1 = 1 << 2,
	//Dev1 = 1 << 3,
	//Dev1_2 = 1 << 4,
	CS2 = 1 << 5,
	LZ2 = 1 << 6,
	HF2 = 1 << 7,
	//Dev2 = 1 << 8,
	CS3 = 1 << 9,
	LZ3 = 1 << 10,
	HF3 = 1 << 11,
	//Dev3 = 1 << 12,
	CS4 = 1 << 13,
	LZ4 = 1 << 14,
	HF4 = 1 << 15,
	CS5 = 1 << 16,
	LZ5 = 1 << 17,
	HF5 = 1 << 18,
	CS6 = 1 << 19,
	LZ6 = 1 << 20,
	Table = 1 << 21,
	TableV = 1 << 22,
	Diagonal = 1 << 23,
	Diagonal2 = 1 << 24,
	Spiral = 1 << 25,
	Enline = 1 << 26,
	AHF = 1 << 31,
}

public static partial class Global
{
	public const byte ProgramVersion = 1;
	public static int BWTBlockSize { get; set; } = 50000;
#pragma warning disable CS0652 // Сравнение с константой интеграции бесполезно: константа находится за пределами диапазона типа
	public static int BWTBlockExtraSize => BWTBlockSize <= 0x4000 ? 2 : BWTBlockSize <= 0x400000 ? 3 : BWTBlockSize <= 0x40000000 ? 4 : BWTBlockSize <= 0x4000000000 ? 5 : BWTBlockSize <= 0x400000000000 ? 6 : BWTBlockSize <= 0x40000000000000 ? 7 : 8;
#pragma warning restore CS0652 // Сравнение с константой интеграции бесполезно: константа находится за пределами диапазона типа
	public static UsedMethodsI PresentMethodsI { get; set; } = UsedMethodsI.CS1 | UsedMethodsI.HF1 | UsedMethodsI.LZ1 | UsedMethodsI.CS2 | UsedMethodsI.HF2 | UsedMethodsI.LZ2;
}

public static partial class DecodingI
{
	public static Image<Bgra32> Decode(byte[] compressedFile, out bool transparency)
	{
		transparency = false;
		if (compressedFile.Length <= 1)
			return new(1, 1);
		var encoding_version = compressedFile[0];
		if (encoding_version == 0)
			return Image.Load<Bgra32>(compressedFile.AsSpan(1));
		//else if (encoding_version < programVersion)
		//	return Outdated.Decode(encoding_version, string_, form);
		var method = compressedFile[1];
		if (compressedFile.Length <= 3)
			return new(1, 1);
		else if (method >= 8)
			return LWDecode(compressedFile, method % 8);
		ArithmeticDecoder ar = compressedFile[2..];
		var counter = (int)ar.ReadCount() - 3;
		(var width, var height) = ar.DecodeWidthAndHeight();
		var cout = DivRem(DivRem((int)ar.ReadEqual(36), 9, out var bestMethod), 2, out var delta);
		var bwt = DivRem(DivRem(DivRem((int)ar.ReadEqual(24), 2, out var rle), 4, out var hf), 2, out var lz);
		if (bwt == 0)
			counter = width * height;
		var rAlpha = ar.ReadEqual(3);
		var enoughTransparency = bwt != 0 && ar.ReadEqual(2) == 1;
		if (rAlpha != 0)
			transparency = true;
		ImageData imageData = (width, height, rAlpha);
		uint lzRDist, lzMaxDist, lzThresholdDist = 0, lzRLength, lzMaxLength, lzThresholdLength = 0, lzUseSpiralLengths = 0, lzRSpiralLength, lzMaxSpiralLength, lzThresholdSpiralLength = 0;
		MethodDataUnit lzDist = new(), lzLength = new(), lzSpiralLength = new();
		int maxFrequency = 0, frequencyCount = 0;
		NList<uint> arithmeticMap = [];
		List<ShortIntervalList> uniqueLists = [];
		NList<byte> skipped = [];
		Current[0] = 0;
		CurrentMaximum[0] = ProgressBarStep * 8;
		if (lz != 0)
		{
			lzRDist = ar.ReadEqual(3);
			lzMaxDist = ar.ReadCount();
			if (lzMaxDist >= counter)
				throw new DecoderFallbackException();
			if (lzRDist != 0)
				lzThresholdDist = ar.ReadEqual(lzMaxDist + 1);
			lzDist = new(lzRDist, lzMaxDist, lzThresholdDist);
			lzRLength = ar.ReadEqual(3);
			lzMaxLength = ar.ReadCount(16);
			if (lzMaxLength >= counter)
				throw new DecoderFallbackException();
			if (lzRLength != 0)
				lzThresholdLength = ar.ReadEqual(lzMaxLength + 1);
			lzLength = new(lzRLength, lzMaxLength, lzThresholdLength);
			if (lzMaxDist == 0 && lzMaxLength == 0 && ar.ReadEqual(2) == 0)
			{
				lz = 0;
				goto l0;
			}
			lzUseSpiralLengths = ar.ReadEqual(2);
			if (lzUseSpiralLengths == 1)
			{
				lzRSpiralLength = ar.ReadEqual(3);
				lzMaxSpiralLength = ar.ReadCount(16);
				if (lzMaxSpiralLength >= counter)
					throw new DecoderFallbackException();
				if (lzRSpiralLength != 0)
				lzThresholdSpiralLength = ar.ReadEqual(lzMaxSpiralLength + 1);
				lzSpiralLength = new(lzRSpiralLength, lzMaxSpiralLength, lzThresholdSpiralLength);
			}
		}
		l0:
		Current[0] += ProgressBarStep;
		LZData lzData = new(lzDist, lzLength, lzUseSpiralLengths, lzSpiralLength);
		List<ShortIntervalList> compressedList;
		if (hf >= 3)
		{
			compressedList = ar.DecodePPM();
			goto l1;
		}
		if (hf >= 2)
		{
			compressedList = ar.DecodeAdaptive(lzData, lz, bwt, delta, skipped, counter);
			goto l1;
		}
		if (hf != 0)
		{
			var counter2 = 4;
			maxFrequency = (int)ar.ReadCount() + 1;
			arithmeticMap.Add((uint)maxFrequency);
			frequencyCount = (int)ar.ReadCount() + 1;
			if (maxFrequency > counter || frequencyCount > counter)
				throw new DecoderFallbackException();
			Status[0] = 0;
			StatusMaximum[0] = frequencyCount;
			ShortIntervalList list;
			uint @base, prev = (uint)maxFrequency;
			for (var i = 0; i < frequencyCount; i++, Status[0]++)
			{
				counter2 += 4;
				list = [];
				@base = bwt != 0 ? ValuesInByte : rAlpha == 0 ? 1 : rAlpha == 1 ? 2u : ValuesInByte;
				list.Add(new(ar.ReadEqual(@base), @base));
				@base = rAlpha != 0 && list[0].Lower == 0 || bwt != 0 ? 1u : ValuesInByte;
				for (var j = bwt == 0 ? 0 : 3; j < 3; j++)
					list.Add(new(ar.ReadEqual(@base), @base));
				uniqueLists.Add(list);
				if (i == 0) continue;
				prev = ar.ReadEqual(prev) + 1;
				counter2++;
				arithmeticMap.Add(arithmeticMap[^1] + prev);
			}
			if (lz != 0)
				arithmeticMap.Add(GetBaseWithBuffer(arithmeticMap[^1], true));
			counter -= bwt == 0 ? 0 : GetArrayLength(counter2, 8);
			if (bwt != 0)
			{
				var skippedCount = (int)ar.ReadCount();
				for (var i = 0; i < skippedCount; i++)
					skipped.Add((byte)ar.ReadEqual(ValuesInByte));
				counter -= (skippedCount + 9) / 8;
			}
		}
		else
		{
			arithmeticMap.AddRange(new Chain(0, (int)(rAlpha == 0 ? 1 : rAlpha == 1 ? 2u : ValuesInByte)).Convert(x => (uint)(x + 1)));
			if (lz != 0)
				arithmeticMap.Add(GetBaseWithBuffer(arithmeticMap[^1], true));
		}
		Current[0] += ProgressBarStep;
		HuffmanData huffmanData = new(maxFrequency, frequencyCount, arithmeticMap, uniqueLists);
		compressedList = ar.ReadCompressedList(imageData, huffmanData, lzData, hf, lz, bwt, delta, counter);
	l1:
		if (bwt != 0)
			compressedList = new BWTDec(compressedList, rAlpha, delta, enoughTransparency).Decode(skipped);
		Current[0] += ProgressBarStep;
		if (rle != 0)
			compressedList = compressedList.DecodeRLE();
		if (compressedList.Length != width * height)
			throw new DecoderFallbackException();
		Current[0] += ProgressBarStep;
		if (delta != 0)
			compressedList.DecodeDelta();
		Current[0] += ProgressBarStep;
		if (rAlpha == 0)
			compressedList.ForEach((x, index) => compressedList[index] = new ShortIntervalList(x) { [0] = new(ValuesInByte - 1, ValuesInByte) });
		else if (rAlpha == 1)
			compressedList.ForEach((x, index) => compressedList[index] = new ShortIntervalList(x) { [0] = new(x[0].Lower == 0 ? 0u : ValuesInByte - 1, ValuesInByte) });
		Current[0] += ProgressBarStep;
		var colorsList = compressedList.DecodeTraversal(bestMethod, width, height);
		Current[0] += ProgressBarStep;
		Image<Bgra32> image = new(width, height);
		colorsList.ForEach((x, index) => x.ForEach((y, index2) => image[index2, index] = new((byte)y[1].Lower, (byte)y[2].Lower, (byte)y[3].Lower, (byte)y[0].Lower)));
		return image;
	}

	private static List<ShortIntervalList> DecodeAdaptive(this ArithmeticDecoder ar, LZData lzData, int lz, int bwt, int delta, NList<byte> skipped, int counter)
	{
		if (bwt != 0)
		{
			var skippedCount = (int)ar.ReadCount();
			for (var i = 0; i < skippedCount; i++)
				skipped.Add((byte)ar.ReadEqual(ValuesInByte));
			counter -= (skippedCount + 9) / 8;
		}
		var leftSerie = 0;
		List<int> values = [];
		if (bwt != 0)
			counter = (int)ar.ReadCount();
		uint fileBase = ar.ReadCount(), base2 = ar.ReadCount(), base3 = ar.ReadCount(), base4 = ar.ReadCount();
		if (counter is < 0)
			throw new DecoderFallbackException();
		Status[0] = 0;
		StatusMaximum[0] = counter;
		SumSet<(uint, uint, uint, uint)> set = [((uint.MaxValue, 0, 0, 0), 1)];
		SumList lengthsSL = lz != 0 ? new(RedStarLinq.Fill(1, (int)(lzData.Length.R == 0 ? lzData.Length.Max + 1 : lzData.Length.R == 1 ? lzData.Length.Threshold + 2 : lzData.Length.Max - lzData.Length.Threshold + 2))) : new(), distsSL = lz != 0 ? new(RedStarLinq.Fill(1, (int)lzData.UseSpiralLengths + 1)) : new();
		var firstIntervalDist = lz != 0 ? (lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max + 1) + lzData.UseSpiralLengths : 0;
		List<(Interval, Interval, Interval, Interval)> uniqueList = [];
		if (lz != 0)
		{
			set.Add(((fileBase - 1, 0, 0, 0), 1));
			uniqueList.Add((new(fileBase - 1, fileBase), new(), new(), new()));
		}
		List<ShortIntervalList> result = [];
		var deltaSum = 0;
		var lzLength = 0;
		while (counter > 0)
		{
			var readItem = ar.ReadPart(set);
			if (readItem == set.Length - 1)
			{
				var Item1 = ar.ReadEqual(fileBase);
				deltaSum = delta == 0 || result.Length == 0 ? (byte)Item1 : unchecked((byte)(deltaSum + Item1 + (ValuesInByte >> 1)));
				var newItem = (Item1, ar.ReadEqual(base2), ar.ReadEqual(base3), ar.ReadEqual(base4));
				if (!set.TryAdd((newItem, 1), out readItem))
					throw new DecoderFallbackException();
				uniqueList.Insert(readItem, (new Interval(newItem.Item1, fileBase - (lz != 0 ? 1u : 0)), new(newItem.Item2, base2), new(newItem.Item3, base3), new(newItem.Item4, base4)));
			}
			else
			{
				var Item1 = uniqueList[readItem].Item1.Lower;
				set.Increase((Item1, uniqueList[readItem].Item2.Lower, uniqueList[readItem].Item3.Lower, uniqueList[readItem].Item4.Lower));
				deltaSum = delta == 0 || result.Length == 0 ? (byte)Item1 : unchecked((byte)(deltaSum + Item1 + (ValuesInByte >> 1)));
			}
			set.Update((uint.MaxValue, 0, 0, 0), Max(set.Length - 1, 1));
			if (bwt != 0)
			{
				result.Add([uniqueList[readItem].Item1]);
				counter--;
				continue;
			}
			if (!(lz != 0 && uniqueList[readItem].Item1.Lower == fileBase - 1))
			{
				ShortIntervalList list = [uniqueList[readItem].Item1, uniqueList[readItem].Item2, uniqueList[readItem].Item3, uniqueList[readItem].Item4];
				if (leftSerie > 0)
				{
					leftSerie--;
					counter--;
					Status[0]++;
					values.Add(1);
				}
				else
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
				}
				result.Add(list);
				lzLength++;
				if (lz != 0 && distsSL.Length < firstIntervalDist)
					distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1);
				continue;
			}
			uint dist, length, spiralLength = 0;
			readItem = ar.ReadPart(lengthsSL);
			lengthsSL.Increase(readItem);
			if (lzData.Length.R == 0)
				length = (uint)readItem;
			else if (lzData.Length.R == 1)
			{
				length = (uint)readItem;
				if (length == lzData.Length.Threshold + 1)
					length += ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold);
			}
			else
			{
				length = (uint)readItem + lzData.Length.Threshold;
				if (length == lzData.Length.Max + 1)
					length = ar.ReadEqual(lzData.Length.Threshold);
			}
			var maxDist = Min(lzData.Dist.Max, (uint)(result.Length - length - 2));
			readItem = ar.ReadPart(distsSL);
			distsSL.Increase(readItem);
			if (lzData.Dist.R == 0 || maxDist < lzData.Dist.Threshold)
				dist = (uint)readItem;
			else if (lzData.Dist.R == 1)
			{
				dist = (uint)readItem;
				if (dist == lzData.Dist.Threshold + 1)
					dist += ar.ReadEqual(maxDist - lzData.Dist.Threshold + lzData.UseSpiralLengths);
			}
			else
			{
				dist = (uint)readItem/* + lzData.Dist.Threshold*/;
				//if (dist == maxDist + 1)
				//{
				//	dist = ar.ReadEqual(lzData.Dist.Threshold + lzData.UseSpiralLengths);
				//	if (dist == lzData.Dist.Threshold)
				//		dist = maxDist + 1;
				//}
			}
			if (dist == maxDist + 1)
			{
				dist = 0;
				if (lzData.SpiralLength.R == 0)
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Max + 1);
				else if (lzData.SpiralLength.R == 1)
				{
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold + 2);
					if (spiralLength == lzData.SpiralLength.Threshold + 1)
						spiralLength += ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold);
				}
				else
				{
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold + 2) + lzData.SpiralLength.Threshold;
					if (spiralLength == lzData.SpiralLength.Max + 1)
						spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold);
				}
			}
			var start = (int)(result.Length - dist - length - 2);
			if (start < 0)
				throw new DecoderFallbackException();
			var fullLength = (int)((length + 2) * (spiralLength + 1));
			for (var i = fullLength; i > 0; i -= (int)length + 2)
			{
				var length2 = (int)Min(length + 2, i);
				result.AddRange(result.GetRange(start, length2));
				var valuesRange = values.GetRange(start, length2);
				values.AddRange(valuesRange);
				var decrease = valuesRange.Sum();
				counter -= decrease;
				Status[0] += decrease;
			}
			if (leftSerie >= fullLength)
				leftSerie -= fullLength;
			else
			{
				var mainCount = 4;
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
				if (newSerie >= ValuesInByte >> 1)
					leftSerie = value - 2 - (result.Length - findIndex - 1);
				else if (result.Length == findIndex + 1)
					leftSerie = 0;
				else
					throw new DecoderFallbackException();
				if (newSerie < ValuesInByte >> 1 || delta == 0)
					deltaSum += unchecked((byte)((value - 1) * (result[findIndex][0].Lower + (ValuesInByte >> 1))));
			}
			lzLength++;
			if (lz != 0 && distsSL.Length < firstIntervalDist)
				new Chain((int)Min(firstIntervalDist - distsSL.Length, (length + 2) * (spiralLength + 1))).ForEach(x => distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1));
		}
		Current[0] += ProgressBarStep;
		return result;
	}

	private static List<ShortIntervalList> ReadCompressedList(this ArithmeticDecoder ar, ImageData imageData, HuffmanData huffmanData, LZData lzData, int hf, int lz, int bwt, int delta, int counter)
	{
		var leftSerie = 0;
		uint colorCount;
		List<int> values = [];
		Status[0] = 0;
		StatusMaximum[0] = counter;
		List<ShortIntervalList> result = [];
		var deltaSum = 0;
		var startingArithmeticMap = lz == 0 ? huffmanData.ArithmeticMap : huffmanData.ArithmeticMap[..^1];
		while (counter > 0)
		{
			var readIndex = ar.ReadPart(result.Length < 2 ? startingArithmeticMap : huffmanData.ArithmeticMap);
			if (bwt != 0)
			{
				result.Add(new(huffmanData.UniqueLists[readIndex]));
				counter--;
				continue;
			}
			if (lz != 0 && readIndex == huffmanData.ArithmeticMap.Length - 1)
			{
				uint dist, length, spiralLength = 0;
				if (lzData.Length.R == 0)
					length = ar.ReadEqual(lzData.Length.Max + 1);
				else if (lzData.Length.R == 1)
				{
					length = ar.ReadEqual(lzData.Length.Threshold + 2);
					if (length == lzData.Length.Threshold + 1)
						length += ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold);
				}
				else
				{
					length = ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold + 2) + lzData.Length.Threshold;
					if (length == lzData.Length.Max + 1)
						length = ar.ReadEqual(lzData.Length.Threshold);
				}
				if (length > result.Length - 2)
					throw new DecoderFallbackException();
				var maxDist = Min(lzData.Dist.Max, (uint)(result.Length - length - 2));
				if (lzData.Dist.R == 0 || maxDist < lzData.Dist.Threshold)
					dist = ar.ReadEqual(maxDist + lzData.UseSpiralLengths + 1);
				else if (lzData.Dist.R == 1)
				{
					dist = ar.ReadEqual(lzData.Dist.Threshold + 2);
					if (dist == lzData.Dist.Threshold + 1)
						dist += ar.ReadEqual(maxDist - lzData.Dist.Threshold + lzData.UseSpiralLengths);
				}
				else
				{
					dist = ar.ReadEqual(maxDist - lzData.Dist.Threshold + 2) + lzData.Dist.Threshold;
					if (dist == maxDist + 1)
					{
						dist = ar.ReadEqual(lzData.Dist.Threshold + lzData.UseSpiralLengths);
						if (dist == lzData.Dist.Threshold)
							dist = maxDist + 1;
					}
				}
				if (dist == maxDist + 1)
				{
					if (lzData.SpiralLength.R == 0)
						spiralLength = ar.ReadEqual(lzData.SpiralLength.Max + 1);
					else if (lzData.SpiralLength.R == 1)
					{
						spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold + 2);
						if (spiralLength == lzData.SpiralLength.Threshold + 1)
							spiralLength += ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold);
					}
					else
					{
						spiralLength = ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold + 2) + lzData.SpiralLength.Threshold;
						if (spiralLength == lzData.SpiralLength.Max + 1)
							spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold);
					}
					dist = 0;
				}
				var start = (int)(result.Length - dist - length - 2);
				if (start < 0)
					throw new DecoderFallbackException();
				var fullLength = (int)((length + 2) * (spiralLength + 1));
				for (var i = fullLength; i > 0; i -= (int)length + 2)
				{
					var length2 = (int)Min(length + 2, i);
					result.AddRange(result.GetRange(start, length2));
					var valuesRange = values.GetRange(start, length2);
					values.AddRange(valuesRange);
					var decrease = valuesRange.Sum();
					counter -= decrease;
					Status[0] += decrease;
				}
				if (leftSerie >= fullLength)
					leftSerie -= fullLength;
				else
				{
					var mainCount = 4;
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
					if (newSerie >= ValuesInByte >> 1)
						leftSerie = value - 2 - (result.Length - findIndex - 1);
					else if (result.Length == findIndex + 1)
						leftSerie = 0;
					else
						throw new DecoderFallbackException();
				}
			}
			else
			{
				ShortIntervalList list = hf == 0 ? new() { new((uint)readIndex, imageData.RAlpha == 2 ? ValuesInByte : imageData.RAlpha + 1) } : new(huffmanData.UniqueLists[readIndex]);
				if (hf == 0)
				{
					deltaSum = delta == 0 || result.Length == 0 ? (byte)list[0].Lower : (byte)((deltaSum + list[0].Lower + (imageData.RAlpha == 2 ? ValuesInByte >> 1 : 1)) % (imageData.RAlpha == 2 ? ValuesInByte : imageData.RAlpha + 1));
					colorCount = imageData.RAlpha != 0 && deltaSum == 0 ? 1u : ValuesInByte;
					for (var j = 0; j < 3; j++)
						list.Add(new(ar.ReadEqual(colorCount), colorCount));
				}
				if (leftSerie > 0)
				{
					leftSerie--;
					counter--;
					Status[0]++;
					values.Add(1);
				}
				else
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
					if (hf == 0 && (newSerie < ValuesInByte >> 1 || delta == 0))
						deltaSum += (byte)((value - 1) * (list[0].Lower + (imageData.RAlpha == 2 ? ValuesInByte >> 1 : 1)) % (imageData.RAlpha == 2 ? ValuesInByte : imageData.RAlpha + 1));
				}
				result.Add(list);
			}
		}
		return result;
	}

	public static void ProcessLZDist(this ArithmeticDecoder ar, LZData lzData, SumList distsSL, int fullLength, out int readIndex, out uint dist, uint length, out uint maxDist)
	{
		maxDist = Min(lzData.Dist.Max, (uint)(fullLength - length - 2));
		readIndex = ar.ReadPart(distsSL);
		distsSL.Increase(readIndex);
		if (lzData.Dist.R == 0 || maxDist < lzData.Dist.Threshold)
			dist = (uint)readIndex;
		else if (lzData.Dist.R == 1)
		{
			dist = (uint)readIndex;
			if (dist == lzData.Dist.Threshold + 1)
				dist += ar.ReadEqual(maxDist - lzData.Dist.Threshold + lzData.UseSpiralLengths);
		}
		else
		{
			dist = (uint)readIndex + lzData.Dist.Threshold;
			if (dist != maxDist + 1)
				return;
			dist = ar.ReadEqual(lzData.Dist.Threshold + lzData.UseSpiralLengths);
			if (dist == lzData.Dist.Threshold)
				dist = lzData.Dist.Max + 1;
		}
	}

	private static List<ShortIntervalList> DecodePPM(this ArithmeticDecoder ar)
	{
		var intervalList = new List<Interval>();
		var (inputBase, base2, base3, base4) = (1u, (uint)ValuesInByte, (uint)ValuesInByte, (uint)ValuesInByte);
		var (inputLength, LZDictionarySize) = ((int)ar.ReadCount(), (int)ar.ReadCount());
		Status[0] = 0;
		StatusMaximum[0] = inputLength;
		List<ShortIntervalList> result = new(inputLength);
		SumSet<(uint, uint, uint, uint)>? set = [], excludingSet = [];
		SumSet<(uint, uint, uint, uint)> globalSet = [];
		var maxDepth = inputBase == 2 ? 96 : 12;
		NList<(uint, uint, uint, uint)> context = new(maxDepth), context2 = new(maxDepth);
		var comparer = new NListEComparer<(uint, uint, uint, uint)>();
		FastDelHashSet<NList<(uint, uint, uint, uint)>> contextHS = new(comparer);
		HashList<int> lzhl = [];
		List<SumSet<(uint, uint, uint, uint)>> sumSets = [];
		SumList lzLengthsSL = [1];
		uint lzCount = 1, notLZCount = 1;
		for (var i = 0; i < inputLength; i++, Status[0]++)
		{
			result.GetSlice(Max(0, i - maxDepth)..i).ForEach((x, index) => context.SetOrAdd(index, (x[0].Lower, x[1].Lower, x[2].Lower, x[3].Lower)));
			context.Reverse();
			context2.Replace(context);
			var item = (uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);
			if (context.Length == maxDepth && i >= maxDepth && ProcessLZ(context, i))
				goto l1;
			set.Clear();
			excludingSet.Clear();
			for (; context.Length > 0 && !contextHS.TryGetIndexOf(context, out _); context.RemoveAt(^1)) ;
			var arithmeticIndex = -1;
			for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out var index) && (arithmeticIndex = set.Replace(sumSets[index]).ExceptWith(excludingSet).Length == 0 ? 1 : ar.ReadPart(new NList<uint>(2, (uint)set.ValuesSum, (uint)(set.ValuesSum + set.Length * 100)))) == 1; context.RemoveAt(^1), excludingSet.UnionWith(set)) ;
			if (set.Length == 0 || context.Length == 0)
			{
				excludingSet.IntersectWith(globalSet).ForEach(x => excludingSet.Update(x.Key, globalSet.TryGetValue(x.Key, out var newValue) ? newValue : throw new EncoderFallbackException()));
				var set2 = globalSet.ExceptWith(excludingSet);
				if (set2.Length != 0 && (arithmeticIndex = ar.ReadPart(new NList<uint>(2, (uint)set2.ValuesSum, (uint)(set2.ValuesSum + set2.Length * 100)))) != 1)
				{
					if (set2.Length != 0) arithmeticIndex = ar.ReadPart(set2);
					item = set2[arithmeticIndex].Key;
				}
				else
					item = (ar.ReadEqual(inputBase), ar.ReadEqual(base2), ar.ReadEqual(base3), ar.ReadEqual(base4));
				globalSet.UnionWith(excludingSet);
			}
			else
			{
				if (set.Length != 0) arithmeticIndex = ar.ReadPart(set);
				item = set[arithmeticIndex].Key;
			}
			result.Add([new(item.Item1, inputBase), new(item.Item2, base2), new(item.Item3, base3), new(item.Item4, base4)]);
		l1:
			var contextLength = context2.Length;
			Increase(context2, context, item, out var hlIndex);
			if (contextLength == maxDepth)
				lzhl.SetOrAdd((i - maxDepth) % LZDictionarySize, hlIndex);
		}
		bool ProcessLZ(NList<(uint, uint, uint, uint)> context, int curPos)
		{
			if (ar.ReadPart(new NList<uint>(2, notLZCount, lzCount + notLZCount)) == 0)
			{
				notLZCount++;
				return false;
			}
			lzCount++;
			var dist = (uint)(curPos - ar.ReadEqual((uint)Min(curPos - maxDepth, LZDictionarySize - 1)) - maxDepth - 2);
			var length = ar.ReadPart(lzLengthsSL);
			if (length < lzLengthsSL.Length - 1)
				lzLengthsSL.Increase(length);
			else
			{
				lzLengthsSL.Increase(lzLengthsSL.Length - 1);
				length = ar.ReadFibonacci(out var fibonacci) ? (int)(fibonacci + lzLengthsSL.Length - 2) : throw new DecoderFallbackException();
			}
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
		return result;
	}

	private static List<ShortIntervalList> DecodeRLE(this List<ShortIntervalList> input)
	{
		Status[0] = 0;
		StatusMaximum[0] = input.Length;
		List<ShortIntervalList> result = [];
		for (var i = 0; i < input.Length; i++, Status[0]++)
		{
			var list = input[i];
			if (list.Length <= 4)
				result.Add(list);
			else if (list[4].Lower >= ValuesInByte >> 1)
				result.Add(new(list.Take(4)));
			else if (list.Length == 5)
			{
				ShortIntervalList list2 = new(list.Take(4));
				for (var j = 0; j < list[4].Lower + 1; j++)
					result.Add(list2);
			}
			else if (list.Length == 7)
			{
				ShortIntervalList list2 = new(list.Take(4));
				for (var j = 0; j < (list[5].Lower << 8) + list[6].Lower + (ValuesInByte >> 1); j++)
					result.Add(list2);
			}
			else
				throw new DecoderFallbackException();
		}
		return result;
	}

	private static void DecodeDelta(this List<ShortIntervalList> input)
	{
		Status[0] = 0;
		StatusMaximum[0] = input.Length;
		for (var i = 1; i < input.Length; i++, Status[0]++)
			input[i] = new((input[i][0].Lower + input[i - 1][0].Lower) % input[i][0].Base == input[i][0].Base / 2 && input[i][0].Base != 1 ? [new((input[i][0].Lower + input[i][0].Base / 2 + input[i - 1][0].Lower) % input[i][0].Base, input[i][0].Base), Interval.Default, Interval.Default, Interval.Default] : input[i].Convert((x, index) => new Interval((x.Lower + x.Base / 2 + input[i - 1][index].Lower) % x.Base, x.Base)));
	}

	private static List<List<ShortIntervalList>> DecodeTraversal(this List<ShortIntervalList> input, int bestMethod, int width, int height)
	{
		switch (bestMethod)
		{
			case 0 or 1:
			Current[0] += ProgressBarStep;
			return input.SplitIntoEqual(width).DecodeEnlining(bestMethod);
			case 2 or 3:
			Current[0] += ProgressBarStep;
			return input.SplitIntoEqual(height).DecodeEnlining(bestMethod).Transpose();
			case >= 4 and <= 7:
			Status[0] = 0;
			StatusMaximum[0] = width + height - 1;
			List<List<ShortIntervalList>> diagonals = [];
			var sum = 0;
			var minDimension = Min(width, height);
			for (var i = 1; i < minDimension; i++, Status[0]++)
				diagonals.Add(input[sum..(sum += i)]);
			int lastTriangle = input.Length - sum, pause = (lastTriangle - sum) / minDimension;
			while (sum < lastTriangle)
			{
				diagonals.Add(input[sum..(sum += minDimension)]);
				Status[0]++;
				if (sum > lastTriangle)
					throw new DecoderFallbackException();
			}
			for (var i = minDimension - 1; i > 0; i--, Status[0]++)
				diagonals.Add(input[sum..(sum += i)]);
			if (sum != input.Length)
				throw new DecoderFallbackException();
			Status[0] = 0;
			StatusMaximum[0] = width + height - 1;
			Current[0] += ProgressBarStep;
			diagonals.DecodeEnlining(bestMethod);
			var result = RedStarLinq.Fill(height, _ => new List<ShortIntervalList>(width));
			for (var i = 0; i < minDimension - 1; i++, Status[0]++)
				diagonals[i].ForEach((x, index) => result[i - index].Add(x));
			var exceedingWidth = width - minDimension;
			for (var i = 0; i < pause; i++, Status[0]++)
				diagonals[minDimension - 1 + i].ForEach((x, index) => result[(width >= height ? 0 : i) + minDimension - 1 - index].Add(x));
			for (var i = 0; i < minDimension - 1; i++, Status[0]++)
				diagonals[^(minDimension - 1 - i)].ForEach((x, index) => result[^(index + 1)].Add(x));
			if (bestMethod >= 6)
				result.Reverse();
			return result;
			case 8:
			Status[0] = 0;
			StatusMaximum[0] = input.Length;
			Current[0] += ProgressBarStep;
			var index = 0;
			List<(int X, int Y)> start = [(0, 0), (width - 1, 1), (width - 2, height - 1), (0, height - 2)];
			List<int> length = [width, height - 1, width - 1, height - 2];
			List<(int X, int Y)> direction = [(1, 0), (0, 1), (-1, 0), (0, -1)];
			List<(int X, int Y)> reduction = [(1, 1), (-1, 1), (-1, -1), (1, -1)];
			var result2 = RedStarLinq.Fill(height, _ => RedStarLinq.EmptyList<ShortIntervalList>(width));
			while (length[0] > 0 && length[1] > 0)
			{
				for (var i = 0; i < 4; i++)
				{
					if (length[i] <= 0)
						continue;
					for (int j = start[i].Y, k = start[i].X, k2 = 0; k2 < length[i]; j += direction[i].Y, k += direction[i].X, k2++)
						result2[j][k] = input[index++];
					start[i] = (start[i].X + reduction[i].X, start[i].Y + reduction[i].Y);
					length[i] -= 2;
				}
			}
			if (index != input.Length)
				throw new DecoderFallbackException();
			return result2;
			default:
				throw new DecoderFallbackException();
		}
	}

	public static List<List<T>> DecodeEnlining<T>(this List<List<T>> input, int bestMethod)
	{
		if (bestMethod % 2 == 0)
			return input;
		var rev = true;
		foreach (var list in input)
		{
			if (rev = !rev)
				list.Reverse();
		}
		return input;
	}

	private static Image<Bgra32> LWDecode(byte[] originalFile, int method)
	{
		ArithmeticDecoder ar = originalFile;
		(var width, var height) = ar.DecodeWidthAndHeight();
		return new(width, height);
	}

	private static (int width, int height) DecodeWidthAndHeight(this ArithmeticDecoder ar)
	{
		var height = (int)ar.ReadCount() + 1;
		var width = (int)ar.ReadCount() + 1;
		if (width is < 16 || height is < 16 || width * height > 0x300000)
			throw new DecoderFallbackException();
		return (width, height);
	}

	private static uint ReadCount(this ArithmeticDecoder ar, uint maxT = 31)
	{
		var temp = (int)ar.ReadEqual(maxT);
		var read = ar.ReadEqual((uint)1 << Max(temp, 1));
		return read + ((temp == 0) ? 0 : (uint)1 << Max(temp, 1));
	}
}

public struct HuffmanData(int maxFrequency, int frequencyCount, NList<uint> arithmeticMap, List<ShortIntervalList> uniqueLists)
{
	public int MaxFrequency { get; private set; } = maxFrequency;
	public int FrequencyCount { get; private set; } = frequencyCount;
	public NList<uint> ArithmeticMap { get; private set; } = arithmeticMap;
	public List<ShortIntervalList> UniqueLists { get; private set; } = uniqueLists;

	public readonly void Deconstruct(out int MaxFrequency, out int FrequencyCount, out NList<uint> ArithmeticMap, out List<ShortIntervalList> UniqueLists)
	{
		MaxFrequency = this.MaxFrequency;
		FrequencyCount = this.FrequencyCount;
		ArithmeticMap = this.ArithmeticMap;
		UniqueLists = this.UniqueLists;
	}
}
