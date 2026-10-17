"""Generate the versioned, dependency-free API contract. Tests compare operation IDs with live routes."""
from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
schemas = {}
def obj(name, properties, required=()):
    schemas[name] = {"type": "object", "properties": properties, "additionalProperties": False}
    if required: schemas[name]["required"] = list(required)
def ref(name): return {"$ref": "#/components/schemas/" + name}
def array(name): return {"type": "array", "items": ref(name)}
s = {"type": "string"}; i = {"type": "integer", "format": "int32"}; money = {"type": "number", "format": "decimal"}
b = {"type": "boolean"}; dt = {"type": "string", "format": "date-time"}
obj("LoginRequest", {"username": {"type":"string", "pattern":"^[a-zA-Z0-9_.-]{3,64}$"}, "password": {"type":"string", "maxLength":128, "writeOnly":True}}, ("username","password"))
obj("UserInfo", {"id":i,"username":s,"role":{"type":"string","enum":["Admin","Operator","Viewer"]}}, ("id","username","role"))
obj("LoginResponse", {"accessToken":s,"expiresAt":dt,"user":ref("UserInfo"),"tokenType":s}, ("accessToken","expiresAt","user","tokenType"))
obj("Product", {"id":i,"sku":{**s,"maxLength":50},"name":{**s,"maxLength":200},"category":{**s,"maxLength":100},
    "purchasePrice":{**money,"minimum":0,"maximum":10000000,"multipleOf":0.01},"price":{**money,"exclusiveMinimum":True,"minimum":0,"maximum":10000000,"multipleOf":0.01},
    "quantity":{**i,"minimum":0,"maximum":1000000},"minQuantity":{**i,"minimum":0,"maximum":1000000},"updatedAt":dt,
    "isLow":{**b,"readOnly":True},"stockValue":{**money,"readOnly":True},"status":{**s,"readOnly":True}}, ("sku","name","price"))
obj("Sale", {"id":i,"createdAt":dt,"total":money,"itemsCount":i})
obj("SaleItem", {"id":i,"saleId":i,"productId":{**i,"nullable":True},"productName":s,"quantity":i,"price":money,"costPrice":money,"sum":{**money,"readOnly":True}})
obj("SaleLineRequest", {"productId":{**i,"minimum":1},"quantity":{**i,"minimum":1,"maximum":1000000}}, ("productId","quantity"))
obj("CreateSaleRequest", {"items":{**array("SaleLineRequest"),"minItems":1,"maxItems":100}}, ("items",))
obj("SaleReceipt", {"id":i,"total":money}, ("id","total"))
obj("PriceChange", {"changedAt":dt,"oldPrice":money,"newPrice":money,"diff":{**money,"readOnly":True},"diffText":{**s,"readOnly":True}})
obj("DashboardStats", {"productsCount":i,"lowStockCount":i,"unitsInStock":i,"stockValue":money,"todaySales":i,"todayRevenue":money,"todayProfit":money})
obj("RevenueDay", {"day":dt,"total":money})
obj("StockRequest", {"quantity":{**i,"minimum":1,"maximum":1000000}}, ("quantity",))
obj("CreateUserRequest", {"username":schemas["LoginRequest"]["properties"]["username"],"password":{**s,"minLength":12,"maxLength":128,"writeOnly":True},"role":schemas["UserInfo"]["properties"]["role"]}, ("username","password","role"))
obj("SetPasswordRequest", {"password":{**s,"minLength":12,"maxLength":128,"writeOnly":True}}, ("password",))
obj("SetActiveRequest", {"active":b}, ("active",))
obj("ApiProblem", {"type":s,"title":s,"status":i,"detail":s,"traceId":s})
obj("IdResponse", {"id":i}, ("id",)); obj("ExistsResponse", {"exists":b}, ("exists",))
obj("HealthResponse", {"status":s}, ("status",))
paths={}
def parameter(name, schema, required=False, location="query"):
    return {"name":name,"in":location,"required":required,"schema":schema}
page=[parameter("offset",{**i,"minimum":0,"default":0}),parameter("limit",{**i,"minimum":1,"maximum":200,"default":100})]
def route(method,path,operation,response=None,body=None,params=(),status=200,role="Authenticated",public=False,description=""):
    responses={str(status):{"description":"Success"}}
    if response: responses[str(status)]["content"]={"application/json":{"schema":response}}
    for code in [400,401,403,404,409,413,429,500]:
        responses[str(code)]={"description":str(code),"content":{"application/json":{"schema":ref("ApiProblem")}}}
    op={"operationId":operation,"summary":operation,"description":description,"responses":responses,"x-required-role":role}
    if public: op["security"]=[]
    if params: op["parameters"]=list(params)
    if "{id}" in path: op.setdefault("parameters",[]).append(parameter("id",{**i,"minimum":1},True,"path"))
    if body: op["requestBody"]={"required":True,"content":{"application/json":{"schema":ref(body)}}}
    paths.setdefault(path,{})[method]=op
route("get","/health","Health",ref("HealthResponse"),public=True)
p="/api/v1"
route("post",p+"/auth/login","Login",ref("LoginResponse"),"LoginRequest",public=True,description="10 attempts/IP/minute; 5 failures/account lock login for 15 minutes.")
route("get",p+"/auth/me","Me",ref("UserInfo")); route("post",p+"/auth/logout","Logout",status=204)
route("get",p+"/products","Products",array("Product"),params=[parameter("search",{**s,"maxLength":200}),parameter("category",{**s,"maxLength":100}),parameter("lowOnly",{**b,"default":False}),*page])
route("get",p+"/products/categories","Categories",{"type":"array","items":s})
route("get",p+"/products/sku-exists","SkuExists",ref("ExistsResponse"),params=[parameter("sku",{**s,"maxLength":50},True),parameter("excludeId",{**i,"minimum":0,"default":0})])
route("get",p+"/products/{id}","Product",ref("Product"))
route("post",p+"/products","CreateProduct",ref("IdResponse"),"Product",status=201,role="Admin or Operator")
route("put",p+"/products/{id}","UpdateProduct",body="Product",status=204,role="Admin or Operator",description="Send id and unchanged updatedAt from the last GET. Stale versions return 409.")
route("delete",p+"/products/{id}","DeleteProduct",status=204,role="Admin")
route("post",p+"/products/{id}/stock","AddStock",body="StockRequest",status=204,role="Admin or Operator")
route("get",p+"/products/{id}/price-history","PriceHistory",array("PriceChange"))
route("get",p+"/sales","Sales",array("Sale"),params=[parameter("from",{**s,"format":"date"},True),parameter("to",{**s,"format":"date"},True),*page],description="Inclusive dates, at most 366 days between dates. Dates use the store server's business day.")
route("get",p+"/sales/recent","RecentSales",array("Sale"),params=[parameter("count",{**i,"minimum":1,"maximum":100,"default":12})])
route("get",p+"/sales/{id}/items","SaleItems",array("SaleItem"))
route("post",p+"/sales","CreateSale",ref("SaleReceipt"),"CreateSaleRequest",params=[parameter("Idempotency-Key",{**s,"format":"uuid"},True,"header")],role="Admin or Operator",description="UUID D format. Reuse the same key and same basket on retries. Same key with different products/quantities returns 409. Price and cost always come from the server.")
route("get",p+"/dashboard/stats","Stats",ref("DashboardStats"))
route("get",p+"/dashboard/revenue","Revenue",array("RevenueDay"),params=[parameter("days",{**i,"minimum":1,"maximum":366,"default":7})])
route("get",p+"/users","Users",array("UserInfo"),role="Admin")
route("post",p+"/users","CreateUser",ref("UserInfo"),"CreateUserRequest",status=201,role="Admin")
route("put",p+"/users/{id}/password","SetPassword",body="SetPasswordRequest",status=204,role="Admin",description="Revokes every session of this user.")
route("put",p+"/users/{id}/active","SetActive",body="SetActiveRequest",status=204,role="Admin",description="Revokes every session of this user. Current admin cannot disable their own account.")
route("get",p+"/openapi.json","OpenApi",{"type":"object"})
contract={"openapi":"3.0.3","info":{"title":"StockFlow HTTPS API","version":"2.0.0","description":"Authenticated inventory, sales and account API. Opaque bearer sessions, HTTPS required."},
    "servers":[{"url":"https://localhost:7071"}],"security":[{"bearerSession":[]}],"paths":paths,
    "components":{"securitySchemes":{"bearerSession":{"type":"http","scheme":"bearer","description":"Opaque session token returned by /api/v1/auth/login (not a JWT)."}},"schemas":schemas}}
(root/'Inventory.Server/openapi.json').write_text(json.dumps(contract,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'Generated {sum(len(v) for v in paths.values())} operations and {len(schemas)} schemas.')
