﻿using System;
using System.IO;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles.Tiles;

namespace watermarking
{
    internal class Program
    {

        static int CountIncludes(VectorTile tile, Polygon polygon)
        {
            var includes = 0;
            foreach (var layer in tile.Layers)
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



        static bool IsLessThanHalfEmptySquare(VectorTile tile, Envelope envelope, double m, int i, int countPoint)
        {
            var countSquare = 0;
            var countEmptySqure = 0;
            for (var j = 0; j < i; j++)
            {
                for (var k = 0; k < i; k++)
                {
                    var p = new Polygon(
                        new LinearRing(
                            new Coordinate[]
                            {
                                    new(envelope.MinX + m * j, envelope.MinY + m * k),
                                    new(envelope.MinX + m * j, envelope.MinY + m * (k + 1)),
                                    new(envelope.MinX + m * (j + 1), envelope.MinY + m * (k + 1)),
                                    new(envelope.MinX + m * (j + 1), envelope.MinY + m * k),
                                    new(envelope.MinX + m * j, envelope.MinY + m * k)
                            }
                    )
                    );

                    if (CountIncludes(tile, p) < countPoint)
                        countEmptySqure++;
                    countSquare++;
                }
            }
            Console.WriteLine($"count square: {countSquare}, count empty square: {countEmptySqure}, {(double)countEmptySqure / countSquare}");
            if ((double)countEmptySqure / countSquare < 0.5)
                return true;
            return false;

        }

        static int[,] GenerateWinx(int m, int n, int key)
        {
            var r = (int)Math.Floor((double)m * m / n);
            Console.WriteLine($"r = {r}");
            Random random = new Random(key);
            var winx = new int[m, m];

            for (int i = 0; i < m; i++)
                for (int j = 0; j < m; j++)
                    winx[i, j] = -1;


            for (var i = 0; i < n; i++)
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

        static bool CheckNearestPoints(int[,] map, int x, int y, int value, int extent)
        {
            if (x < 0 || x > extent || y < 0 || y > extent)
                return false;

            if (x + 1 < extent)
                if (map[x + 1, y] != value)
                    return true;

            if (x - 1 > 0)
                if (map[x - 1, y] != value)
                    return true;

            if (y + 1 < extent)
                if (map[x, y + 1] != value)
                    return true;

            if (y - 1 > 0)
                if (map[x, y - 1] != value)
                    return true;

            return false;
        }

        static bool CheckMapPoint(int[,] map, int dist, int x, int y, int extent)
        {
            var value = map[x, y];

            if (CheckNearestPoints(map, x, y, value, extent))
                return true;
            for (var i = 1; i < dist; ++i)
            {
                if (CheckNearestPoints(map, x + i, y, value, extent))
                    return true;
                if (CheckNearestPoints(map, x - i, y, value, extent))
                    return true;
                if (CheckNearestPoints(map, x, y + i, value, extent))
                    return true;
                if (CheckNearestPoints(map, x, y - i, value, extent))
                    return true;
            }
            return false;

        }

        static int[,] ChangeMap(int[,] map, int dist, int extent)
        {
            for (var i = 0; i < extent; i++)
                for (var j = 0; j < extent; j++)
                    if (!CheckMapPoint(map, dist, i, j, extent))
                        Console.WriteLine($"{i}, {j}, {map[i, j]}");
            return map;
        }

        static int[,] GenerateMap(int key, int dist, int extent = 4096)
        {
            var map = new int[extent, extent];
            Random random = new Random(key);
            for (var i = 0; i < extent; i++)
                for (var j = 0; j < extent; j++)
                    map[i, j] = random.Next() % 2;
            map = ChangeMap(map, dist, extent);
            return map;
        }

        static void Main(string[] args)
        {
            var path = "C:\\Users\\user\\source\\Watermarking\\828.mvt";
            using var fileStream = new FileStream(path, FileMode.Open);

            var z = 11;
            var x = 359;
            var y = 828;
            var reader = new MapboxTileReader();
            var tile = reader.Read(fileStream, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

            var env = CoordinateConverter.TileBounds(x, y, z);
            Console.WriteLine(env);
            Console.WriteLine(env.Height);
            Console.WriteLine(env.Width);

            var envMeters = CoordinateConverter.DegreesToMeters(env);

            Console.WriteLine(envMeters);
            Console.WriteLine(envMeters.Height);
            Console.WriteLine(envMeters.Width);

            var a = 0.0;
            var m = 20;
            for (; m >= 2; m--)
            {
                a = envMeters.Height / m;
                Console.WriteLine($"a = {a}, m = {m}");

                if (IsLessThanHalfEmptySquare(tile, envMeters, a, m, 20))
                    break;
            }

            Console.WriteLine($"a = {a}, m = {m}");

            var key = 0;

            var winx = GenerateWinx(m, 5, key);

            for (var i = 0; i < m; i++)
            {
                for (var j = 0; j < m; j++)
                {
                    Console.Write($"{winx[i, j]} ");
                }
                Console.WriteLine();
            }

            var map = GenerateMap(0, 1, 10);

            var count1 = 0;
            var count0 = 0;

            for (var i = 0; i < 10; i++)
            {
                for (var j = 0; j < 10; j++)
                {
                    if (map[i, j] == 1)
                        count1++;
                    else
                        count0++;
                }
            }

            for (var i = 0; i < 10; i++)
            {
                for (var j = 0; j < 10; j++)
                {
                    Console.Write($"{map[i, j]} ");
                }
                Console.WriteLine();
            }

            Console.WriteLine($"count 0: {count0}, count : {count1}");

            //Console.WriteLine(CoordinateConverter.MapSize(z));
            //var coor_min = CoordinateConverter.PositionToGlobalPixel(new double[] { env.MinX, env.MinY }, z);
            //var coor_max = CoordinateConverter.PositionToGlobalPixel(new double[] { env.MaxX, env.MaxY }, z);
            //Console.WriteLine($"{coor_min[0]}, {coor_min[1]}");
            //Console.WriteLine($"{coor_max[0]}, {coor_max[1]}");
            //Console.WriteLine($"{coor_max[0] - coor_min[0]}, {coor_max[1] - coor_min[1]}");
            //Console.WriteLine(tile.TileId);

            //foreach (var layer in tile.Layers)
            //{
            //    Console.WriteLine(layer.Name);
            //    foreach (var feature in layer.Features)
            //    {
            //        Console.WriteLine($"Geometry type:{feature.Geometry.GeometryType}");
            //        foreach (var point in feature.Geometry.Coordinates)
            //            Console.WriteLine($"({point.X}, {point.Y})\n");
            //    }
            //}
        }
    }
}
