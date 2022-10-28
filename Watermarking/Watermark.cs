using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Watermarking
{
    public class Watermark
    {
        public int M { get; set; }
        public VectorTile Tile { get; set; }

        private readonly Envelope _envelopeTile;

        private double _a;

        private readonly int _extent;

        private readonly int _sizeMessage;

        private readonly double _extentDist;

        private readonly int _countPoints;

        private readonly int _distance;

        private readonly int[,] _winx;

        private readonly bool[,] _map;

        private int CountIncludes(Polygon polygon)
        {
            var includes = 0;
            foreach (var layer in Tile.Layers)
            {
                foreach (var feature in layer.Features)
                {
                    foreach (var point in feature.Geometry.Coordinates)
                    {
                        var pointMeters = CoordinateConverter.DegreesToMeters(point);
                        if (polygon.Intersects(new Point(pointMeters)))
                            includes++;
                    }
                }
            }
            return includes;
        }

        private bool IsLessThanHalfEmptySquare(double a, int m, int countPoint)
        {
            var countSquare = 0;
            var countEmptySqure = 0;
            for (var i = 0; i < m; i++)
            {
                for (var j = 0; j < m; j++)
                {
                    var polygon = new Polygon(
                        new LinearRing(
                            new Coordinate[]
                            {
                                    new(_envelopeTile.MinX + a * i, _envelopeTile.MinY + a * j),
                                    new(_envelopeTile.MinX + a * i, _envelopeTile.MinY + a * (j + 1)),
                                    new(_envelopeTile.MinX + a * (i + 1), _envelopeTile.MinY + a * (j + 1)),
                                    new(_envelopeTile.MinX + a * (i + 1), _envelopeTile.MinY + a * j),
                                    new(_envelopeTile.MinX + a * i, _envelopeTile.MinY + a * j)
                            }
                    )
                    );

                    if (CountIncludes(polygon) < countPoint)
                        countEmptySqure++;
                    countSquare++;
                }
            }
            Console.WriteLine($"count square: {countSquare}, count empty square: {countEmptySqure}, {(double)countEmptySqure / countSquare}");
            if ((double)countEmptySqure / countSquare < 0.5)
                return true;
            return false;
        }

        private void PartitionTile(int countPoints)
        {
            var a = 0.0;
            var m = 20;
            for (; m >= 2; m--)
            {
                a = _envelopeTile.Height / m;
                Console.WriteLine($"a = {a}, m = {m}");

                if (IsLessThanHalfEmptySquare(a, m, countPoints))
                    break;
            }
            M = m;
            _a = a;
        }

        private int[,] GenerateWinx(int key)
        {
            var r = (int)Math.Floor((double)M * M / _sizeMessage);
            Console.WriteLine($"r = {r}");
            var random = new Random(key);
            var winx = new int[M, M];

            for (int i = 0; i < M; i++)
                for (int j = 0; j < M; j++)
                    winx[i, j] = -1;


            for (var i = 0; i < _sizeMessage; i++)
            {
                for (var j = 0; j < r; j++)
                {
                    int x;
                    int y;
                    do
                    {
                        x = random.Next() % M;
                        y = random.Next() % M;
                    } while (winx[x, y] != -1);

                    winx[x, y] = i;
                }
            }

            return winx;
        }

        private bool[,] GenerateMap(int key)
        {
            var map = new bool[_extent, _extent];
            var random = new Random(key);
            for (var i = 0; i < _extent; i++)
                for (var j = 0; j < _extent; j++)
                    map[i, j] = Convert.ToBoolean(random.Next() % 2);
            map = ChangeMap(map);
            return map;
        }

        private bool[,] ChangeMap(bool[,] map)
        {
            var count = 0;
            for (var i = 0; i < _extent; i++)
                for (var j = 0; j < _extent; j++)
                    if (!CheckMapPoint(map, i, j))
                    {
                        ++count;
                        if (Convert.ToInt32(map[i, j]) == 0)
                            map[i, j] = Convert.ToBoolean(1);
                        else
                            map[i, j] = Convert.ToBoolean(0);
                    }
            return map;
        }

        private bool CheckMapPoint(bool[,] map, int x, int y)
        {
            var value = Convert.ToInt32(map[x, y]);

            if (CheckNearestPoints(map, x, y, value))
                return true;

            for (var i = 1; i < _distance; ++i)
            {

                if (CheckNearestPoints(map, x + i, y, value))
                    return true;
                if (CheckNearestPoints(map, x - i, y, value))
                    return true;
                if (CheckNearestPoints(map, x, y + i, value))
                    return true;
                if (CheckNearestPoints(map, x, y - i, value))
                    return true;

            }
            return false;
        }

        private bool CheckNearestPoints(bool[,] map, int x, int y, int value)
        {
            if (x < 0 || x >= _extent || y < 0 || y >= _extent)
                return false;

            if (x + 1 < _extent)
                if (Convert.ToInt32(map[x + 1, y]) != value)
                    return true;

            if (x - 1 >= 0)
                if (Convert.ToInt32(map[x - 1, y]) != value)
                    return true;

            if (y + 1 < _extent)
                if (Convert.ToInt32(map[x, y + 1]) != value)
                    return true;

            if (y - 1 >= 0)
                if (Convert.ToInt32(map[x, y - 1]) != value)
                    return true;

            return false;
        }

        private void GetOppositePoint(int x, int y, int value, out int xRes, out int yRes)
        {
            xRes = -1;
            yRes = -1;

            if (x + 1 < _extent)
                if (Convert.ToInt32(_map[x + 1, y]) != value)
                {
                    xRes = x + 1;
                    yRes = y;
                    return;
                }

            if (x - 1 >= 0)
                if (Convert.ToInt32(_map[x - 1, y]) != value)
                {
                    xRes = x - 1;
                    yRes = y;
                    return;
                }

            if (y + 1 < _extent)
                if (Convert.ToInt32(_map[x, y + 1]) != value)
                {
                    xRes = x;
                    yRes = y + 1;
                    return;
                }

            if (y - 1 >= 0)
                if (Convert.ToInt32(_map[x, y - 1]) != value)
                {
                    xRes = x;
                    yRes = y - 1;
                    return;
                }
        }

        //return list of point
        private void FindOppositeIndex(int value, int x, int y, out int xRes, out int yRes)
        {
            xRes = -1;
            yRes = -1;

            if (CheckNearestPoints(_map, x, y, value))
            {
                GetOppositePoint(x, y, value, out xRes, out yRes);
            }

            for (var i = 1; i < _distance; ++i)
            {

                if (CheckNearestPoints(_map, x + i, y, value))
                {
                    GetOppositePoint(x + i, y, value, out xRes, out yRes);
                    return;
                }

                if (CheckNearestPoints(_map, x - i, y, value))
                {
                    GetOppositePoint(x - i, y, value, out xRes, out yRes);
                    return;
                }

                if (CheckNearestPoints(_map, x, y + i, value))
                {
                    GetOppositePoint(x, y + 1, value, out xRes, out yRes);
                    return;
                }

                if (CheckNearestPoints(_map, x, y - i, value))
                {
                    GetOppositePoint(x, y - 1, value, out xRes, out yRes);
                    return;
                };

            }
        }

        private double Statistics(Polygon polygon, out int s0, out int s1)
        {
            s0 = 0;
            s1 = 0;

            foreach (var layer in Tile.Layers)
                foreach (var feature in layer.Features)
                {
                    var geometry = feature.Geometry;
                    var coordinates = geometry.Coordinates;
                    foreach (var coordinate in coordinates)
                    {
                        var coordinateMeters = CoordinateConverter.DegreesToMeters(coordinate);
                        if (polygon.Contains(new Point(coordinateMeters)))
                        {
                            var x = (int)Convert.ToInt32((coordinateMeters.X - _envelopeTile.MinX) / _extentDist);
                            var y = (int)Convert.ToInt32((coordinateMeters.Y - _envelopeTile.MinY) / _extentDist);
                            if (x == _extent || y == _extent)
                                continue;
                            var mapValue = Convert.ToInt32(_map[x, y]);

                            //Console.WriteLine($"x: {x}, y: {y}, value:{mapValue}, x m: {coordinateMeters.X}, y m: {coordinateMeters.Y}, x m: {coordinate.X}, y m: {coordinate.Y}");
                            //if (mapValue == 0)
                            // Console.WriteLine($"{(coordinateMeters.X - _envelopeTile.MinX) / _extentDist}, {(coordinateMeters.Y - _envelopeTile.MinY) / _extentDist}");
                            if (mapValue == 1)
                                s1++;
                            else
                                s0++;

                        }
                    }
                }

            if ((s0 == 0 && s1 == 0) || s0 + s1 < _countPoints)
                return -1;

            return (double)Math.Abs(s0 - s1) / (s1 + s0);
        }

        //check for valid feature, if not valid chose another point
        private void ChangeCoordinate(int value, int count, int s, Polygon polygon)
        {
            var step = (int)Math.Floor((double)s / count);
            var count_changed = 0;
            foreach (var layer in Tile.Layers)
            {
                foreach (var feature in layer.Features)
                {
                    var geometry = feature.Geometry;
                    var coordinates = geometry.Coordinates;
                    for (var j = 0; j < coordinates.Length; j++)
                    {
                        if (count_changed / step >= count)
                            return;

                        var coordinateMeters = CoordinateConverter.DegreesToMeters(coordinates[j]);
                        if (polygon.Contains(new Point(coordinateMeters)))
                        {
                            var x = Convert.ToInt32((coordinateMeters.X - _envelopeTile.MinX) / _extentDist);
                            var y = Convert.ToInt32((coordinateMeters.Y - _envelopeTile.MinY) / _extentDist);

                            if (x == _extent || y == _extent)
                                continue;

                            var mapValue = Convert.ToInt32(_map[x, y]);
                            if (mapValue == value)
                                continue;

                            if (count_changed % step == 0)
                            {
                                FindOppositeIndex(mapValue, x, y, out int xNew, out int yNew);

                                double xMeteres = _envelopeTile.MinX + xNew * _extentDist;
                                double yMeteres = _envelopeTile.MinY + yNew * _extentDist;

                                var coor = CoordinateConverter.MetersToDegrees(new Coordinate(xMeteres, yMeteres));

                                if (x != xNew)
                                    geometry.Coordinates[j].X = coor.X;
                                if (y != yNew)
                                    geometry.Coordinates[j].Y = coor.Y;

                                //Console.WriteLine($"{feature.Attributes.GetValues()[0]} j: {j}, x: {x}, y: {y}, x new:{xNew}, y new: {yNew}, x m: {xMeteres}, y m: {yMeteres}, x d: {coor.X}, y d: {coor.Y}, value: {value}");

                                for (var k = j + 1; k < coordinates.Length; k++)
                                {
                                    var coordinate = CoordinateConverter.DegreesToMeters(coordinates[k]);
                                    if (coordinate.X == coordinateMeters.X && coordinate.Y == coordinateMeters.Y)
                                    {
                                        geometry.Coordinates[k].X = coor.X;
                                        geometry.Coordinates[k].Y = coor.Y;
                                        count_changed++;
                                    }

                                }
                            }

                            count_changed++;
                        }
                    }
                }
            }
        }

        public Watermark(string path, int x, int y, int z, int key, int sizeMessage, int distance = 2, int countPoints = 20, int extent = 4096, int m = 0)
        {
            _extent = extent;
            _countPoints = countPoints;
            _distance = distance;
            _sizeMessage = sizeMessage;

            using var fileStream = new FileStream(path, FileMode.Open);
            var reader = new MapboxTileReader();
            Tile = reader.Read(fileStream, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

            var env = CoordinateConverter.TileBounds(x, y, z);
            _envelopeTile = CoordinateConverter.DegreesToMeters(env);
            _extentDist = _envelopeTile.Height / _extent;
            if (m == 0)
                PartitionTile(countPoints);
            else
            {
                M = m;
                _a = _envelopeTile.Height / m;
            }

            _winx = GenerateWinx(key);
            _map = GenerateMap(key);

        }

        public Watermark(VectorTile tile, int x, int y, int z, int key, int sizeMessage, int distance = 2, int countPoints = 20, int extent = 4096, int m = 0)
        {
            _extent = extent;
            _countPoints = countPoints;
            _distance = distance;
            _sizeMessage = sizeMessage;

            Tile = tile;

            var env = CoordinateConverter.TileBounds(x, y, z);
            _envelopeTile = CoordinateConverter.DegreesToMeters(env);
            _extentDist = _envelopeTile.Height / _extent;
            if (m == 0)
                PartitionTile(countPoints);
            else
            {
                M = m;
                _a = _envelopeTile.Height / m;
            }

            _winx = GenerateWinx(key);
            _map = GenerateMap(key);

        }

        public Watermark(byte[] tile, int x, int y, int z, int key, int sizeMessage, int distance = 2, int countPoints = 20, int extent = 4096, int m = 0, bool compressed = false)
        {
            _extent = extent;
            _countPoints = countPoints;
            _distance = distance;
            _sizeMessage = sizeMessage;

            using var memoryStream = new MemoryStream(tile);
            var reader = new MapboxTileReader();

            memoryStream.Seek(0, SeekOrigin.Begin);

            if (compressed)
            {
                using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
                Tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));
            }
            else
            {
                Tile = reader.Read(memoryStream, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));
            }

            var env = CoordinateConverter.TileBounds(x, y, z);
            _envelopeTile = CoordinateConverter.DegreesToMeters(env);
            _extentDist = _envelopeTile.Height / _extent;
            if (m == 0)
                PartitionTile(countPoints);
            else
            {
                M = m;
                _a = _envelopeTile.Height / m;
            }

            _winx = GenerateWinx(key);
            _map = GenerateMap(key);

        }

        public void Embed(BitArray bites, double t2, double delta2)
        {
            //var bites = new BitArray(bytes);
            for (var i = 0; i < M; i++)
                for (var j = 0; j < M; j++)
                {

                    var index = _winx[i, j];
                    if (index == -1)
                        continue;
                    var value = Convert.ToInt32(bites[index]);

                    var polygon = new Polygon(
                        new LinearRing(
                            new Coordinate[]
                            {
                                    new(_envelopeTile.MinX + _a * i, _envelopeTile.MinY + _a * j),
                                    new(_envelopeTile.MinX + _a * i, _envelopeTile.MinY + _a * (j + 1)),
                                    new(_envelopeTile.MinX + _a * (i + 1), _envelopeTile.MinY + _a * (j + 1)),
                                    new(_envelopeTile.MinX + _a * (i + 1), _envelopeTile.MinY + _a * j),
                                    new(_envelopeTile.MinX + _a * i, _envelopeTile.MinY + _a * j)
                            }
                    )
                    );



                    var stat = Statistics(polygon, out int s0, out int s1);
                    if (stat == -1)
                    {
                        Console.WriteLine($"i: {i}, j:{j}, s0: {s0}, s1: {s1}, index: {index}, not embeded");
                        continue;
                    }

                    Console.WriteLine($"i: {i}, j:{j}, s0: {s0}, s1: {s1}, stat: {stat}, value: {value}, index: {index}");
                    if (stat >= t2 + delta2)
                    {
                        if (s1 - s0 > 0 && value == 1)
                        {
                            Console.WriteLine($"yes");
                            continue;
                        }
                        if (s0 - s1 > 0 && value == 0)
                        {
                            Console.WriteLine($"yes");
                            continue;
                        }
                        Console.WriteLine($"no");
                    }

                    if (value == 1)
                    {
                        var countAdded = (int)Math.Ceiling(((s0 + s1) * (t2 + delta2) + s0 - s1) / 2);
                        Console.WriteLine($"needed to 1: {countAdded}");
                        ChangeCoordinate(1, countAdded, s0, polygon);
                    }

                    if (value == 0)
                    {
                        var countAdded = (int)Math.Ceiling(((s0 + s1) * (t2 + delta2) + s1 - s0) / 2);
                        Console.WriteLine($"needed to 0: {countAdded}");
                        ChangeCoordinate(0, countAdded, s1, polygon);
                    }
                }
        }

        public BitArray GetWatermark(double t2)
        {
            var bits = new BitArray(_sizeMessage, false);
            var dict = new Dictionary<int, int>(_sizeMessage);

            for (var i = 0; i < _sizeMessage; i++)
                dict.Add(i, 0);

            for (var i = 0; i < M; i++)
                for (var j = 0; j < M; j++)
                {

                    var index = _winx[i, j];
                    if (index == -1)
                        continue;

                    var polygon = new Polygon(
                        new LinearRing(
                            new Coordinate[]
                            {
                                    new(_envelopeTile.MinX + _a * i, _envelopeTile.MinY + _a * j),
                                    new(_envelopeTile.MinX + _a * i, _envelopeTile.MinY + _a * (j + 1)),
                                    new(_envelopeTile.MinX + _a * (i + 1), _envelopeTile.MinY + _a * (j + 1)),
                                    new(_envelopeTile.MinX + _a * (i + 1), _envelopeTile.MinY + _a * j),
                                    new(_envelopeTile.MinX + _a * i, _envelopeTile.MinY + _a * j)
                            }
                    )
                    );



                    var stat = Statistics(polygon, out int s0, out int s1);
                    if (stat == -1)
                    {
                        Console.WriteLine($"i: {i}, j:{j}, s0: {s0}, s1: {s1}, index:{index}, not embeded");
                        continue;
                    }

                    Console.WriteLine($"i: {i}, j:{j}, s0: {s0}, s1: {s1}, stat: {stat}, index: {index}");
                    if (stat >= t2)
                    {
                        if (s0 > s1)
                        {
                            Console.WriteLine($"index: {index}, value: {0}");
                            dict[index] -= 1;
                        }

                        if (s1 > s0)
                        {
                            dict[index] += 1;
                            Console.WriteLine($"index: {index}, value: {1}");
                        }
                    }
                }

            for (var i = 0; i < _sizeMessage; i++)
            {
                if (dict[i] > 0)
                    bits[i] = true;
                if (dict[i] < 0)
                    bits[i] = false;
            }

            return bits;
        }

        public int CountOutside()
        {
            var polygon = new Polygon(
                        new LinearRing(
                            new Coordinate[]
                            {
                                    new(_envelopeTile.MinX, _envelopeTile.MinY),
                                    new(_envelopeTile.MinX, _envelopeTile.MaxY),
                                    new(_envelopeTile.MaxX, _envelopeTile.MaxY),
                                    new(_envelopeTile.MaxX, _envelopeTile.MinY),
                                    new(_envelopeTile.MinX, _envelopeTile.MinY)
                            }));
            Console.WriteLine($"polygon: {polygon.Coordinates[0]}, {polygon.Coordinates[1]}, {polygon.Coordinates[2]}, {polygon.Coordinates[3]}");
            var countOutside = 0;

            //var geometry = CoordinateConverter.DegreesToMeters(Tile.Layers[0].Features[0].Geometry.Copy()).Envelope;

            //for (var i = 0; i < Tile.Layers.Count; i++)
            //    for (var j = 1; j < Tile.Layers[i].Features.Count; j++)
            //    {
            //        if (Tile.Layers[i].Features[j].Geometry.IsValid)
            //        {
            //            var g = CoordinateConverter.DegreesToMeters(Tile.Layers[i].Features[j].Geometry.Copy()).Envelope;
            //            geometry = g.Union(geometry);
            //        }
            //    }

            //var bbox = geometry.Envelope;
            //Console.WriteLine(bbox);

            foreach (var layer in Tile.Layers)
                foreach (var feature in layer.Features)
                    foreach (var point in feature.Geometry.Coordinates)
                    {
                        var pointMeters = CoordinateConverter.DegreesToMeters(point);
                        if (!polygon.Contains(new Point(pointMeters)))
                        {
                            countOutside++;
                            //Console.WriteLine($"x y: {pointMeters.X} {pointMeters.Y}");
                            //Console.WriteLine($"{feature.Attributes.Count}");
                        }
                    }
            return countOutside;
        }
    }
}
