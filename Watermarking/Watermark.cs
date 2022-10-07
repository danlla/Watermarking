using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using NetTopologySuite.IO.VectorTiles.Tiles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Threading.Tasks;
using static NetTopologySuite.IO.VectorTiles.Mapbox.Tile;

namespace Watermarking
{
    public class Watermark
    {
        private Envelope _envelopeTile;

        public VectorTile _tile;

        private int _m;

        private double _a;

        private int _extent;

        private double _extentDist;

        private int _countPoints;

        private int _distance;

        private int[,] _winx;

        private int[,] _map;
        //public Watermark(VectorTile tile, int x, int y, int z, int key, int extent = 4096)
        //{

        //}

        private int CountIncludes(Polygon polygon)
        {
            var includes = 0;
            foreach (var layer in _tile.Layers)
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
            _m = m;
            _a = a;
        }

        private int[,] GenerateWinx(int m, int sizeMessage, int key)
        {
            var r = (int)Math.Floor((double)m * m / sizeMessage);
            Console.WriteLine($"r = {r}");
            Random random = new Random(key);
            var winx = new int[m, m];

            for (int i = 0; i < m; i++)
                for (int j = 0; j < m; j++)
                    winx[i, j] = -1;


            for (var i = 0; i < sizeMessage; i++)
            {
                for (var j = 0; j < r; j++)
                {
                    var x = 0;
                    var y = 0;
                    do
                    {
                        x = random.Next() % m;
                        y = random.Next() % m;
                    } while (winx[x, y] != -1);

                    winx[x, y] = i;
                }
            }

            return winx;
        }

        private int[,] GenerateMap(int key)
        {
            var map = new int[_extent, _extent];
            Random random = new Random(key);
            for (var i = 0; i < _extent; i++)
                for (var j = 0; j < _extent; j++)
                    map[i, j] = random.Next() % 2;
            map = ChangeMap(map);
            return map;
        }

        private int[,] ChangeMap(int[,] map)
        {
            var count = 0;
            for (var i = 0; i < _extent; i++)
                for (var j = 0; j < _extent; j++)
                    if (!CheckMapPoint(map, i, j))
                    {
                        ++count;
                        if (map[i, j] == 0)
                            map[i, j] = 1;
                        else
                            map[i, j] = 0;
                    }
            Console.WriteLine($"count problem: {count}");
            return map;
        }

        private bool CheckMapPoint(int[,] map, int x, int y)
        {
            var value = map[x, y];

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

        private bool CheckNearestPoints(int[,] map, int x, int y, int value)
        {
            if (x < 0 || x >= _extent || y < 0 || y >= _extent)
                return false;

            if (x + 1 < _extent)
                if (map[x + 1, y] != value)
                    return true;

            if (x - 1 >= 0)
                if (map[x - 1, y] != value)
                    return true;

            if (y + 1 < _extent)
                if (map[x, y + 1] != value)
                    return true;

            if (y - 1 >= 0)
                if (map[x, y - 1] != value)
                    return true;

            return false;
        }

        private void CheckNearestPoints(int x, int y, int value, out int xRes, out int yRes)
        {
            xRes = -1;
            yRes = -1;

            if (x + 1 < _extent)
                if (_map[x + 1, y] != value)
                {
                    xRes = x + 1;
                    yRes = y;
                    return;
                }

            if (x - 1 >= 0)
                if (_map[x - 1, y] != value)
                {
                    xRes = x - 1;
                    yRes = y;
                    return;
                }

            if (y + 1 < _extent)
                if (_map[x, y + 1] != value)
                {
                    xRes = x;
                    yRes = y + 1;
                    return;
                }

            if (y - 1 >= 0)
                if (_map[x, y - 1] != value)
                {
                    xRes = x;
                    yRes = y - 1;
                    return;
                }
        }

        private void FindOppositeIndex(int value, int x, int y, out int xRes, out int yRes)
        {
            xRes = -1;
            yRes = -1;

            if (CheckNearestPoints(_map, x, y, value))
            {
                CheckNearestPoints(x, y, value, out xRes, out yRes);
            }

            for (var i = 1; i < _distance; ++i)
            {

                if (CheckNearestPoints(_map, x + i, y, value))
                {
                    CheckNearestPoints(x + i, y, value, out xRes, out yRes);
                    return;
                }

                if (CheckNearestPoints(_map, x - i, y, value))
                {
                    CheckNearestPoints(x - i, y, value, out xRes, out yRes);
                    return;
                }

                if (CheckNearestPoints(_map, x, y + i, value))
                {
                    CheckNearestPoints(x, y + 1, value, out xRes, out yRes);
                    return;
                }

                if (CheckNearestPoints(_map, x, y - i, value))
                {
                    CheckNearestPoints(x, y - 1, value, out xRes, out yRes);
                    return;
                };

            }
        }

        private double Statistics(Polygon polygon, out int s0, out int s1)
        {
            s0 = 0;
            s1 = 0;

            foreach (var layer in _tile.Layers)
                foreach (var feature in layer.Features)
                {
                    var geometry = feature.Geometry;
                    var coordinates = geometry.Coordinates;
                    foreach (var coordinate in coordinates)
                    {
                        var coordinateMeters = CoordinateConverter.DegreesToMeters(coordinate);
                        if (polygon.Contains(new Point(coordinateMeters)))
                        {
                            var x = (int)Math.Floor((coordinateMeters.X - _envelopeTile.MinX) / _extentDist);
                            var y = (int)Math.Floor((coordinateMeters.Y - _envelopeTile.MinY) / _extentDist);
                            var mapValue = _map[x, y];

                            //Console.WriteLine($"x: {x}, y: {y}, value: {mapValue}");

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

        private void ChangeCoordinate(int value, int count, int s, Polygon polygon)
        {
            var step = (int)Math.Floor((double)s / count);
            var i = 0;
            foreach (var layer in _tile.Layers)
            {
                foreach (var feature in layer.Features)
                {
                    var geometry = feature.Geometry;
                    var coordinates = geometry.Coordinates;
                    for (var j = 0; j < coordinates.Length; j++)
                    {
                        var coordinateMeters = CoordinateConverter.DegreesToMeters(coordinates[j]);
                        if (polygon.Contains(new Point(coordinateMeters)))
                        {
                            if (i / step == count)
                                return;

                            var x = (int)Math.Floor((coordinateMeters.X - _envelopeTile.MinX) / _extentDist);
                            var y = (int)Math.Floor((coordinateMeters.Y - _envelopeTile.MinY) / _extentDist);
                            var mapValue = _map[x, y];
                            if (mapValue == value)
                                continue;

                            if (i % step == 0)
                            {
                                int xNew;
                                int yNew;
                                FindOppositeIndex(mapValue, x, y, out xNew, out yNew);

                                //Console.WriteLine($"i: {i}, step: {step},  value: {value}, map value: {_map[x,y]} finding value: {_map[xNew, yNew]}, x: {x}, y: {y}, x new: {xNew}, y new: {yNew}");


                                double xMeteres = _envelopeTile.MinX + xNew * _extentDist + _extentDist / 2;

                                double yMeteres = _envelopeTile.MinY + yNew * _extentDist + _extentDist / 2;

                                //var metcoor = CoordinateConverter.DegreesToMeters(coordinates[j]);

                                //Console.WriteLine($"coor: {metcoor}, x new: {xMeteres}, y new: {yMeteres}, dif x: {metcoor.X - xMeteres}, dif y: {metcoor.Y - yMeteres}, extentDist: {_extentDist}");

                                var coor = CoordinateConverter.MetersToDegrees(new Coordinate(xMeteres, yMeteres));

                                //Console.WriteLine($"x: {coordinateMeters.X}, y: {coordinateMeters.Y},  x new: {xMeteres}, y new: {yMeteres}");

                                //Console.WriteLine($"xcoor: {coordinates[j].X}, ycoor: {coordinates[j].Y},  xcoor new: {coor.X}, ycoor new: {coor.Y}");

                                if (x != xNew)
                                    geometry.Coordinates[j].X = coor.X;
                                if (y != yNew)
                                    geometry.Coordinates[j].Y = coor.Y;

                                //Console.WriteLine($"x in geometry: {coordinates[j].X}, y in geometry: {coordinates[j].Y}");
                            }

                            i++;
                        }
                    }
                }
            }
        }

        public Watermark(string path, int x, int y, int z, int key, int sizeMessage, int distance = 2, int countPoints = 20, int extent = 4096)
        {
            _extent = extent;
            _countPoints = countPoints;
            _distance = distance;

            using var fileStream = new FileStream(path, FileMode.Open);
            var reader = new MapboxTileReader();
            _tile = reader.Read(fileStream, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

            var env = CoordinateConverter.TileBounds(x, y, z);
            _envelopeTile = CoordinateConverter.DegreesToMeters(env);
            _extentDist = _envelopeTile.Height / _extent;
            PartitionTile(countPoints);

            _winx = GenerateWinx(_m, sizeMessage, key);
            _map = GenerateMap(key);

        }

        public void Embed(byte[] bytes, double t2, double delta2)
        {
            var bites = new BitArray(bytes);
            for (var i = 0; i < _m; i++)
                for (var j = 0; j < _m; j++)
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


                    int s0;
                    int s1;

                    var stat = Statistics(polygon, out s0, out s1);
                    if (stat == -1)
                    {
                        Console.WriteLine($"i: {i}, j:{j}, s0: {s0}, s1: {s1}, not embeded");
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



                    //stat = Statistics(polygon, out s0, out s1);
                    //Console.WriteLine($"i: {i}, j:{j}, s0: {s0}, s1: {s1}, stat: {stat}, value: {value}, index: {index}");
                    //if (stat >= t2 + delta2)
                    //{
                    //    if (s1 - s0 > 0 && value == 1)
                    //    {
                    //        Console.WriteLine($"ALL RIGHT");
                    //        continue;
                    //    }
                    //    if (s0 - s1 > 0 && value == 0)
                    //    {
                    //        Console.WriteLine($"ALL RIGHT");
                    //        continue;
                    //    }
                    //    Console.WriteLine($"NOT ALL RIGHT");
                    //}

                    //Console.WriteLine($"i: {i}, j:{j}, stat: {stat}");

                    //foreach (var layer in _tile.Layers)
                    //    foreach (var feature in layer.Features)
                    //    {
                    //        var geometry = feature.Geometry;
                    //        if (polygon.Intersects(geometry))
                    //        {
                    //            var coordinates = geometry.Coordinates;
                    //            foreach (var coordinate in coordinates)
                    //            {
                    //                if (polygon.Contains(new Point(coordinate)))
                    //                {



                    //                }
                    //            }
                    //        }
                    //    }
                }
        }
    }
}
