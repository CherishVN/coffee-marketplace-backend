using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ECommerceAPI.Infrastructure.Data;

var db = new ApplicationDbContext();
var shop = db.Shops.FirstOrDefault(s => s.Id == Guid.Parse("b6b04bbb-6606-44a3-aa5b-f15820094e1c"));
Console.WriteLine($"Status: {shop.Status}, Verif: {shop.VerificationStatus}, GHN: {shop.GhnShopId}, Phone: {shop.Phone}, Dist: {shop.DistrictId}, Ward: {shop.WardCode}");
