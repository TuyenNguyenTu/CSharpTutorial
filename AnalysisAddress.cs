using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Best.V3.CommonLibrary;
using Best.V3.Domain.Entities.Categories;
using Best.V3.Dto.Models.Categories;
using Best.V3.Dto.Results;
using Best.V3.Dto.Results.Categories.Location;
using Best.V3.Infrastructure.CommonRepository;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Math.EC.Rfc7748;

namespace Best.V3.AddressApi.Factories
{
    public interface IAnalysisAddressServices
    {
        Task<AnalysisAddressResultNew> AnalysisAddressTuningUp(ListAnalysisAddressNewResultItem model);
        Task<AnalysisAddressNewResult> GetAddressByName(AnalysisAddressNewModel model);
    }
    public class AnalysisAddressServices : IAnalysisAddressServices
    {
        private readonly IRepository<City> _cityRepository;
        private readonly IRepository<District> _districtRepository;
        private readonly IRepository<Ward> _wardRepository;
        private readonly IRepository<Dc> _dcRepository;
        public AnalysisAddressServices(IRepository<City> cityRepository,
               IRepository<District> districtRepository, IRepository<Ward> wardRepository, IRepository<Dc> dcRepository)
        {
            _cityRepository = cityRepository;
            _districtRepository = districtRepository;
            _wardRepository = wardRepository;
            _dcRepository = dcRepository;
        }

        public async Task<AnalysisAddressResultNew> AnalysisAddressTuningUp(ListAnalysisAddressNewResultItem model)
        {
            var rs = new AnalysisAddressResultNew() { Result = Result.Success, ListResult = new List<AnalysisAddressNewResultItem>() };
            if (model.AnalysisAddressResultItems.Count > 0)
            {
                var cities = await _cityRepository.Query().OrderBy(c => c.DisplayOrder).Include(c => c.Exceptions)
              .ToListAsync();
                List<AnalysisAddressNewResultItem> citiesRS = new List<AnalysisAddressNewResultItem>();
                foreach (var modelAnalysisAddressItem in model.AnalysisAddressResultItems)
                {
                    AnalysisAddressNewResultItem cityRS = new AnalysisAddressNewResultItem() { Result = Result.Success, Guid = modelAnalysisAddressItem.Guid };
                    var beginNumber = Regex.Match(modelAnalysisAddressItem.Address, @"^\d+").ToString();
                    if (!string.IsNullOrEmpty(beginNumber))
                    {
                        var address = modelAnalysisAddressItem.Address;
                        modelAnalysisAddressItem.Address = modelAnalysisAddressItem.Address.Substring(beginNumber.Length, address.Length - beginNumber.Length);
                    }
                    string currentAddress;
                    cityRS = AnalysisCity(modelAnalysisAddressItem, cities, cityRS, out currentAddress);
                    if (rs.Result != Result.Success) return rs;
                    modelAnalysisAddressItem.Address = currentAddress;
                    citiesRS.Add(cityRS);
                }

                //District
                var cityIds = citiesRS.Where(x => x.CityId > 0).Select(x => x.CityId).Distinct().ToList();
                var districts = await _districtRepository.Query().Where(x => cityIds.Contains(x.CityId)).OrderBy(c => c.DisplayOrder)
                .Include(c => c.Exceptions).ToListAsync();
                List<AnalysisAddressNewResultItem> districtsRS = new List<AnalysisAddressNewResultItem>();
                foreach (var modelAnalysisAddressItem in model.AnalysisAddressResultItems)
                {
                    AnalysisAddressNewResultItem districtRS = new AnalysisAddressNewResultItem() { Result = Result.Success, Guid = modelAnalysisAddressItem.Guid };
                    var cityCheck = citiesRS.FirstOrDefault(x => x.Guid == modelAnalysisAddressItem.Guid);
                    if (cityCheck != null && cityCheck.CityId > 0)
                    {
                        districtRS.CityId = cityCheck.CityId;
                        districtRS.CityName = cityCheck.CityName;
                        districtRS.CityRealm = cityCheck.CityRealm;
                    }
                    var beginNumber = Regex.Match(modelAnalysisAddressItem.Address, @"^\d+").ToString();
                    if (!string.IsNullOrEmpty(beginNumber))
                    {
                        var address = modelAnalysisAddressItem.Address;
                        modelAnalysisAddressItem.Address = modelAnalysisAddressItem.Address.Substring(beginNumber.Length, address.Length - beginNumber.Length);
                    }
                    string currentAddress;
                    var districtCheck = districts.Where(x => x.CityId == districtRS.CityId).ToList();
                    districtRS = AnalysisDistrict(modelAnalysisAddressItem, districtCheck, districtRS, out currentAddress);
                    if (rs.Result != Result.Success) return rs;
                    modelAnalysisAddressItem.Address = currentAddress;
                    districtsRS.Add(districtRS);
                }

                //Ward
                var districtIds = districtsRS.Where(x => x.DistrictId > 0).Select(x => x.DistrictId).Distinct().ToList();
                var wards = await _wardRepository.Query().Where(x => districtIds.Contains(x.DistrictId)).OrderBy(c => c.DisplayOrder).Include(c => c.Exceptions)
                .ToListAsync();
                List<AnalysisAddressNewResultItem> wardsRS = new List<AnalysisAddressNewResultItem>();
                foreach (var modelAnalysisAddressItem in model.AnalysisAddressResultItems)
                {
                    AnalysisAddressNewResultItem wardRS = new AnalysisAddressNewResultItem() { Result = Result.Success, Guid = modelAnalysisAddressItem.Guid };
                    var districtCheck = districtsRS.FirstOrDefault(x => x.Guid == modelAnalysisAddressItem.Guid);
                    if (districtCheck != null && districtCheck.DistrictId > 0)
                    {
                        wardRS.DistrictId = districtCheck.DistrictId;
                        wardRS.DistrictName = districtCheck.DistrictName;
                    }
                    var beginNumber = Regex.Match(modelAnalysisAddressItem.Address, @"^\d+").ToString();
                    if (!string.IsNullOrEmpty(beginNumber))
                    {
                        var address = modelAnalysisAddressItem.Address;
                        modelAnalysisAddressItem.Address = modelAnalysisAddressItem.Address.Substring(beginNumber.Length, address.Length - beginNumber.Length);
                    }
                    var wardCheck = wards.Where(x => x.DistrictId == wardRS.DistrictId).ToList();
                    wardRS = AnalysisWard(modelAnalysisAddressItem, wardCheck, wardRS);
                    if (rs.Result != Result.Success) return rs;
                    wardsRS.Add(wardRS);
                }
                foreach (var modelAnalysisAddressItem in model.AnalysisAddressResultItems)
                {
                    AnalysisAddressNewResultItem resultItem = new AnalysisAddressNewResultItem() { Result = Result.Success, Guid = modelAnalysisAddressItem.Guid };
                    var resultCity = citiesRS.FirstOrDefault(x => x.Guid == modelAnalysisAddressItem.Guid);
                    if (resultCity != null)
                    {
                        resultItem.CityId = resultCity.CityId;
                        resultItem.CityName = resultCity.CityName;
                        resultItem.CityRealm = resultCity.CityRealm;
                    }
                    var resultDistrict = districtsRS.FirstOrDefault(x => x.Guid == modelAnalysisAddressItem.Guid);
                    if (resultDistrict != null)
                    {
                        resultItem.DistrictId = resultDistrict.DistrictId;
                        resultItem.DistrictName = resultDistrict.DistrictName;
                    }
                    var resultWard = wardsRS.FirstOrDefault(x => x.Guid == modelAnalysisAddressItem.Guid);
                    if (resultWard != null)
                    {
                        resultItem.WardId = resultWard.WardId;
                        resultItem.WardName = resultWard.WardName;
                        resultItem.IsOutOfServe = resultWard.IsOutOfServe;
                    }
                    rs.ListResult.Add(resultItem);
                }
            }
            return rs;

        }

        public async Task<AnalysisAddressNewResult> GetAddressByName(AnalysisAddressNewModel model)
        {
            var result = new AnalysisAddressNewResult() { Result = Result.Success };
            var analysisAddressModel = new ListAnalysisAddressNewResultItem
            {
                AnalysisAddressResultItems = new List<AnalysisAddressNewItem>()
            };
            var sourceOrderId = Guid.NewGuid().ToString();
            var destOrderId = Guid.NewGuid().ToString();
            var returnOrderId = Guid.NewGuid().ToString();
            analysisAddressModel.AnalysisAddressResultItems.Add(new AnalysisAddressNewItem
            {
                Guid = sourceOrderId,
                Address = !string.IsNullOrEmpty(model.SourceAddress) ? model.SourceAddress.Trim() : "",
                CityName = !string.IsNullOrEmpty(model.SourceCity) ? model.SourceCity.Trim() : "",
                DistrictName = !string.IsNullOrEmpty(model.SourceDistrict) ? model.SourceDistrict.Trim() : "",
                WardName = !string.IsNullOrEmpty(model.SourceWard) ? model.SourceWard.Trim() : ""
            });
            analysisAddressModel.AnalysisAddressResultItems.Add(new AnalysisAddressNewItem
            {
                Guid = destOrderId,
                Address = !string.IsNullOrEmpty(model.DestAddress) ? model.DestAddress.Trim() : "",
                CityName = !string.IsNullOrEmpty(model.DestCity) ? model.DestCity.Trim() : "",
                DistrictName = !string.IsNullOrEmpty(model.DestDistrict) ? model.DestDistrict.Trim() : "",
                WardName = !string.IsNullOrEmpty(model.DestWard) ? model.DestWard.Trim() : ""
            });
            analysisAddressModel.AnalysisAddressResultItems.Add(new AnalysisAddressNewItem
            {
                Guid = returnOrderId,
                Address = !string.IsNullOrEmpty(model.ReturnAddress) ? model.ReturnAddress.Trim() : "",
                CityName = !string.IsNullOrEmpty(model.ReturnCity) ? model.ReturnCity.Trim() : "",
                DistrictName = !string.IsNullOrEmpty(model.ReturnDistrict) ? model.ReturnDistrict.Trim() : "",
                WardName = !string.IsNullOrEmpty(model.ReturnWard) ? model.ReturnWard.Trim() : ""
            });

            var analysisAddressResult = await AnalysisAddressTuningUp(analysisAddressModel);
            var sourceAddress = analysisAddressResult.ListResult.FirstOrDefault(x => x.Guid == sourceOrderId);
            var destAddress = analysisAddressResult.ListResult.FirstOrDefault(x => x.Guid == destOrderId);
            var returnAddress = analysisAddressResult.ListResult.FirstOrDefault(x => x.Guid == returnOrderId);
            if (sourceAddress != null)
            {
                if (sourceAddress.CityId > 0 && sourceAddress.CityId != null)
                {
                    result.SourceCity = sourceAddress.CityName;
                    result.SourceCityId = sourceAddress.CityId;
                }
                if (sourceAddress.DistrictId > 0 && sourceAddress.DistrictId != null)
                {
                    result.SourceDistrict = sourceAddress.DistrictName;
                    result.SourceDistrictId = sourceAddress.DistrictId;
                }
                if (sourceAddress.WardId > 0 && sourceAddress.WardId != null)
                {
                    result.SourceWard = sourceAddress.WardName;
                    result.SourceWardId = sourceAddress.WardId;
                }
            }
            if (destAddress != null)
            {
                if (destAddress.CityId > 0 && destAddress.CityId != null)
                {
                    result.DestCity = destAddress.CityName;
                    result.DestCityId = destAddress.CityId;
                }
                if (destAddress.DistrictId > 0 && destAddress.DistrictId != null)
                {
                    result.DestDistrict = destAddress.DistrictName;
                    result.DestDistrictId = destAddress.DistrictId;
                }
                if (destAddress.WardId > 0 && destAddress.WardId != null)
                {
                    result.DestWard = destAddress.WardName;
                    result.DestWardId = destAddress.WardId;
                }
            }
            if (returnAddress != null)
            {
                if (returnAddress.CityId > 0 && returnAddress.CityId != null)
                {
                    result.ReturnCity = returnAddress.CityName;
                    result.ReturnCityId = returnAddress.CityId;
                }
                if (returnAddress.DistrictId > 0 && returnAddress.DistrictId != null)
                {
                    result.ReturnDistrict = returnAddress.DistrictName;
                    result.ReturnDistrictId = returnAddress.DistrictId;
                }
                if (returnAddress.WardId > 0 && returnAddress.WardId != null)
                {
                    result.ReturnWard = returnAddress.WardName;
                    result.ReturnWardId = returnAddress.WardId;
                }
            }
            if (destAddress.IsOutOfServe == true || sourceAddress.IsOutOfServe == true || returnAddress.IsOutOfServe == true)
            {
                result.IsOutOfServe = true;
                result.Message = "Ngoài vùng phục vụ";
            }
            if(sourceAddress.CityId == null && destAddress.CityId == null && returnAddress.CityId == null)
            {
                result.Result = Result.Failed;
                result.Message = "Không tìm thấy tỉnh quận phường";
            }
            return result;
        }
        private AnalysisAddressNewResultItem AnalysisCity(AnalysisAddressNewItem item, List<City> cities, AnalysisAddressNewResultItem currentResultItem, out string analysisAddress)
        {
            var rs = currentResultItem;
            analysisAddress = item.Address.Trim().ToLower();
            var city = new City();
            //Nếu tên thành phố có trong dữ liệu truyền vào
            if (!string.IsNullOrEmpty(item.CityName))
            {
                //Loại bỏ những tiền tố, hậu tố thừa đi
                item.CityName = item.CityName.Trim().ToLower();
                var cityTrim = item.CityName.Trim();
                var cityNames = new List<string>() {
                    cityTrim.ToLower(),
                    cityTrim.ToLower().Unsigned(),
                    cityTrim.ToUpper(),
                    cityTrim.ToUpper().Unsigned()
                };
                city =
                    cities.FirstOrDefault(c => cityNames.Any(cn => cn.Contains(c.Name.Trim().ToLower()))
                    || cityNames.Any(cn => cn.Contains(c.UnsignName.ToLower()))
                    || c.Exceptions.Any(ex => cityNames.Contains(ex.Name.ToLower()))
                    || cityNames.Any(cn => cn.Contains(c.Name.Trim().ToUpper()))
                    || cityNames.Any(cn => cn.Contains(c.UnsignName.ToUpper()))
                    || c.Exceptions.Any(ex => cityNames.Contains(ex.Name.ToUpper()))
                    );
            }
            //Nếu không phân tích được ở cột tỉnh / thành phố thì phân tích tiếp ở địa chỉ
            if (city == null || city.Id <= 0)
            {
                //Sắp xếp theo thứ tự hiển thị
                cities = cities.OrderBy(c => c.DisplayOrder).ToList();
                var add = analysisAddress;
                var listCharacter = new List<string>{
                    ",",";","/",";",":","-","_","."
                };
                //Phân tích từ địa chỉ ra
                foreach (var cityCheck in cities)
                {
                    var cityNames = new List<string>() {
                        cityCheck.Name.ToLower(),
                        cityCheck.UnsignName
                    };
                    var cityExceptions = cityCheck.Exceptions.Where(e => !e.IsDeleted).Select(e => e.Name.ToLower()).ToList();
                    if (cityExceptions.Any())
                    {
                        var unsignedExceptions = cityExceptions.Select(e => e.ConvertToUnsign()).ToList();
                        var cityExceptionsUppper = cityCheck.Exceptions.Where(e => !e.IsDeleted).Select(e => e.Name.ToLower()).ToList();
                        cityExceptions.AddRange(unsignedExceptions);
                        cityExceptions.AddRange(cityExceptionsUppper);
                        cityNames.AddRange(cityExceptions);
                    }
                    if (listCharacter.Any(x => add.EndsWith(x))){
                        char c = add.Last();
                        add = add.Trim(c); 
                    }

                    if (!cityNames.Any(w => add.EndsWith(w))) continue;
                    var foundCityName = cityNames.FirstOrDefault(w => add.EndsWith(w));
                    city = cityCheck;
                    if (analysisAddress.LastIndexOf(foundCityName, StringComparison.Ordinal) > 0)
                    {
                        analysisAddress = analysisAddress.Substring(0, analysisAddress.LastIndexOf(foundCityName, StringComparison.Ordinal));
                    }
                    break;
                }
            }
            if (city != null && city.Id > 0)
            {
                rs.CityId = city.Id;
                rs.CityName = city.Name;
            }
            else
            {
                rs.Result = Result.Failed;
                rs.Message = "Không phân tích được thành phố";
            }
            return rs;
        }
        private AnalysisAddressNewResultItem AnalysisDistrict(AnalysisAddressNewItem item, List<District> districts, AnalysisAddressNewResultItem currentResultItem, out string analysisAddress)
        {
            var rs = currentResultItem;
            analysisAddress = item.Address.Trim().ToLower();
            var district = new District();

            //Nếu tên quận / huyện có trong dữ liệu truyền vào
            if (!string.IsNullOrEmpty(item.DistrictName))
            {
                //Loại bỏ những tiền tố, hậu tố thừa đi
                item.DistrictName = item.DistrictName.Trim().ToLower();

                var districtNames = new List<string>() { item.DistrictName, item.DistrictName.ConvertToUnsign() };
                district =
                    districts.FirstOrDefault(c =>
                    districtNames.Any(dn => dn.Contains(c.Name.ToLower()))
                    || districtNames.Any(dn => dn.Contains(c.UnsignName.ToLower()))
                    || c.Exceptions.Any(ex => districtNames.Contains(ex.Name.ToLower()))
                    );
            }
            if (district == null || district.Id <= 0)
            {
                //Sắp xếp theo thứ tự hiển thị
                districts = districts.Where(d => d.CityId == currentResultItem.CityId).OrderBy(c => c.DisplayOrder).ToList();
                if (districts.Any())
                {
                    var add = analysisAddress;
                    var unsignedAdd = analysisAddress.ConvertToUnsign();
                    //Phân tích từ địa chỉ ra
                    foreach (var districtCheck in districts)
                    {
                        var districtCheckName = districtCheck.Name.ToLower();
                        var districtNameToUnsign = districtCheckName.ConvertToUnsign();
                        var districtNames = new List<string>() {
                            "quận "+ districtCheckName
                            ,"quận"+ districtCheckName
                            ,"quận." + districtCheckName
                            ,"quan " + districtCheckName
                            ,"quan." + districtCheckName
                           
                            ,"q" + districtCheckName
                            ,"q " + districtCheckName
                            ,"q." + districtCheckName
                            
                            ,"quận "+ districtNameToUnsign
                            ,"quận"+ districtNameToUnsign
                            ,"quận." + districtNameToUnsign
                            ,"quan " +districtNameToUnsign
                            ,"quan." + districtNameToUnsign

                            ,"q" + districtNameToUnsign
                            ,"q " + districtNameToUnsign
                            ,"q." + districtNameToUnsign
                            ,"q. "+districtNameToUnsign
                        };
                        if (!districtCheckName.IsNumber())
                        {
                            districtNames.Add(districtCheckName);
                            districtNames.Add(districtCheckName.ConvertToUnsign());
                        }
                        var districtExceptions = districtCheck.Exceptions.Where(e => !e.IsDeleted).Select(e => e.Name.ToLower()).ToList();
                        if (districtExceptions.Any())
                        {
                            var unsignedExceptions = districtExceptions.Select(e => e.ConvertToUnsign()).ToList();
                            districtExceptions.AddRange(unsignedExceptions);
                            districtNames.AddRange(districtExceptions);
                        }

                        if (!districtNames.Any(w => add.Contains(w)) && !districtNames.Any(w => unsignedAdd.Contains(w))) continue;
                        var foundDistrictName = districtNames.FirstOrDefault(w => add.Contains(w) || unsignedAdd.Contains(w));
                        district = districtCheck;
                        if (analysisAddress.LastIndexOf(foundDistrictName, StringComparison.Ordinal) > 0)
                        {
                            analysisAddress = analysisAddress.ReplaceLastOccur(foundDistrictName);
                        }
                        break;
                    }
                }
                else
                {
                    rs.Result = Result.Failed;
                    rs.Message = "Không tìm thấy quận / huyện nào";
                }
            }
            if (district != null && district.Id > 0)
            {
                rs.DistrictId = district.Id;
                rs.DistrictName = district.Name;
            }
            else
            {
                rs.Result = Result.Failed;
                rs.Message = "Không phân tích được quận / huyện";
            }
            return rs;
        }
        private AnalysisAddressNewResultItem AnalysisWard(AnalysisAddressNewItem item, List<Ward> wards, AnalysisAddressNewResultItem currentResultItem)
        {
            var rs = currentResultItem;
            var analysisAddress = item.Address.Trim().ToLower();
            var ward = new Ward();
            //Nếu tên quận / huyện có trong dữ liệu truyền vào
            if (!string.IsNullOrEmpty(item.WardName))
            {
                //Loại bỏ những tiền tố, hậu tố thừa đi
                item.WardName = item.WardName.Trim().ToLower();

                var wardNames = new List<string>() { item.WardName, item.WardName.Unsigned() };
                ward =
                    wards.FirstOrDefault(c =>
                    wardNames.Any(wn => wn.Contains(c.Name.ToLower()))
                    || wardNames.Any(wn => wn.Contains(c.UnsignName.ToLower()))
                    || c.Exceptions.Any(ex => wardNames.Contains(ex.Name.ToLower()))
                    );
            }

            //Nếu tên quận / huyện không có trong dữ liệu truyền vào hoặc không phân tích được thì lấy tiếp tới địa chỉ
            if (ward == null || ward.Id <= 0)
            {
                //Sắp xếp theo thứ tự hiển thị
                wards = wards.Where(d => d.DistrictId == currentResultItem.DistrictId).OrderBy(c => c.DisplayOrder).ToList();
                if (wards.Any())
                {
                    var add = analysisAddress;
                    var addUnsigned = analysisAddress.ConvertToUnsign();
                    //Phân tích từ địa chỉ ra
                    foreach (var wardCheck in wards)
                    {
                        var wardCheckName = wardCheck.Name.ToLower();
                        var wardCheckNameToUnsign = wardCheckName.ConvertToUnsign();
                        var wardNames = new List<string>()
                        {
                            "phường "+ wardCheckName
                            ,"phường"+ wardCheckName
                            ,"phường."+ wardCheckName

                            ,"phuong "+ wardCheckName
                            ,"phuong"+ wardCheckName
                            ,"phuong."+ wardCheckName

                            ,"p "+ wardCheckName
                            ,"p"+ wardCheckName
                            ,"p."+ wardCheckName

                            ,"xã "+ wardCheckName
                            ,"xã"+ wardCheckName
                            ,"xã."+ wardCheckName
                            ,"xa "+ wardCheckName
                            ,"xa"+ wardCheckName
                            ,"xa."+ wardCheckName

                            ,"x "+ wardCheckName
                            ,"x"+ wardCheckName
                            ,"x."+ wardCheckName

                            ,wardCheckName

                            ,wardCheckNameToUnsign
                            ,"phuong "+ wardCheckNameToUnsign
                            ,"phuong"+ wardCheckNameToUnsign
                            ,"phuong."+ wardCheckNameToUnsign

                            ,"xa "+ wardCheckNameToUnsign
                            ,"xa"+ wardCheckNameToUnsign
                            ,"xa."+ wardCheckNameToUnsign

                            ,"x "+ wardCheckNameToUnsign
                            ,"x"+ wardCheckNameToUnsign
                            ,"x."+ wardCheckNameToUnsign

                        };
                        var wardExceptions = wardCheck.Exceptions.Where(ex => !ex.IsDeleted).Select(e => e.Name.ToLower()).ToList();
                        if (wardExceptions.Any())
                        {
                            var unsignedExceptions = wardExceptions.Select(e => e.ConvertToUnsign()).ToList();
                            wardExceptions.AddRange(unsignedExceptions);
                            wardNames.AddRange(wardExceptions);
                        }

                        if (!wardNames.Any(w => add.Contains(w)) && !wardNames.Any(w => addUnsigned.Contains(w))) continue;
                        ward = wardCheck;
                        break;
                    }
                }
                else
                {
                    rs.Result = Result.Failed;
                    rs.Message = "Không tìm thấy phường / xã nào";
                }
            }

            if (ward != null && ward.Id > 0)
            {
                var isoutofserve = false;
                if (ward.IsInServe == false)
                {
                    isoutofserve = true;
                }
                rs.WardId = ward.Id;
                rs.WardName = ward.Name;
                rs.IsOutOfServe = isoutofserve;
            }
            else
            {
                rs.Result = Result.Failed;
                rs.Message = "Không phân tích được phường / xã";
            }
            return rs;
        }

    }
}
