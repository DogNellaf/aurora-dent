#!/usr/bin/env python3
"""End-to-end smoke test of a running Aurora Dent instance (standard library only).

    python docker/smoke_test.py http://localhost:8080

It walks the real user journey over HTTP, the same way a browser would: health check, public
pages, sign in as the demo patient (antiforgery token and cookies included), book a free slot,
see it in the cabinet, cancel it, and check that other roles are kept out of each other's pages.
Exit code is 0 when everything passes.
"""
import html
import http.cookiejar
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:8080").rstrip("/")


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


class Session:
    def __init__(self):
        self.jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar), NoRedirect)

    def request(self, path, data=None):
        body = urllib.parse.urlencode(data).encode() if data is not None else None
        try:
            with self.opener.open(urllib.request.Request(BASE + path, data=body), timeout=20) as r:
                return r.status, r.read().decode("utf-8"), r.headers
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode("utf-8", "replace"), e.headers

    def get(self, path):
        return self.request(path)

    def post(self, form_page, action, fields=None):
        _, page, _ = self.get(form_page)
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)
        assert token, f"no antiforgery token on {form_page}"
        return self.request(action, {**(fields or {}), "__RequestVerificationToken": html.unescape(token.group(1))})

    def login(self, email):
        status, _, headers = self.post("/route/login", "/route/login", {"Email": email, "Password": "Demo123!"})
        assert status == 302, f"login as {email} failed with {status}"


failures = []


def check(name, condition, detail=""):
    print(("PASS  " if condition else "FAIL  ") + name + ("" if condition else f"  -> {detail}"))
    if not condition:
        failures.append(name)


def wait_until_healthy(timeout=90):
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            status, body, _ = Session().get("/health")
            if status == 200 and body == "Healthy":
                return True
        except Exception:
            pass
        time.sleep(2)
    return False


check("application becomes healthy", wait_until_healthy())

anon = Session()
for path in ["/", "/services", "/services/1", "/doctors", "/schedule?staffId=1", "/about", "/faq", "/contacts", "/route/login"]:
    status, body, _ = anon.get(path)
    check(f"public page {path}", status == 200 and "Аврора Дент" in body, f"status {status}")

status, body, headers = anon.get("/")
check("security headers are present", headers.get("X-Content-Type-Options") == "nosniff" and "frame-ancestors" in (headers.get("Content-Security-Policy") or ""))
check("anonymous cabinet access redirects to login", anon.get("/client")[0] == 302)
check("unknown page is a friendly 404", anon.get("/definitely/not/here")[0] == 404)

# --- patient journey -------------------------------------------------------------------------
patient = Session()
patient.login("client@clinic.demo")
status, cabinet, _ = patient.get("/client")
check("patient cabinet opens", status == 200 and "Анна" in cabinet)

_, schedule, _ = patient.get("/schedule?serviceId=3&staffId=1")
slots = re.findall(r'name="appointmentId" value="(\d+)"', schedule)
check("free slots are offered", len(slots) > 0)
slot_id = slots[-1]  # the last one is days away, so it can still be cancelled (the cut-off is 2 hours)

before = len(re.findall(r'/client/appointments/\d+/cancel', cabinet))
status, _, headers = patient.post("/schedule?serviceId=3&staffId=1", "/appointments/book", {"appointmentId": slot_id, "serviceId": "3"})
check("booking redirects to the cabinet", status == 302 and headers.get("Location", "").endswith("/client"), f"{status} {headers.get('Location')}")

_, cabinet, _ = patient.get("/client")
cancel_urls = re.findall(r'/client/appointments/(\d+)/cancel', cabinet)
check("new booking is listed in the cabinet", len(cancel_urls) == before + 1)

other = Session()
other.login("igor@clinic.demo")
status, _, headers = other.post("/schedule?staffId=1", "/appointments/book", {"appointmentId": slot_id})
check("the same slot cannot be taken by another patient", status == 302 and "/schedule" in headers.get("Location", ""), f"{status} {headers.get('Location')}")

status, _, _ = patient.post("/client", f"/client/appointments/{slot_id}/cancel")
_, cabinet, _ = patient.get("/client")
check("cancelling frees the slot", status == 302 and f"/client/appointments/{slot_id}/cancel" not in cabinet)

# --- role separation -------------------------------------------------------------------------
check("patient cannot open the admin panel", patient.get("/admin")[0] == 302)

doctor = Session()
doctor.login("doctor@clinic.demo")
status, body, _ = doctor.get("/doctor")
check("doctor dashboard opens", status == 200 and "Расписание на сегодня" in body)

manager = Session()
manager.login("manager@clinic.demo")
check("manager moderation queue opens", manager.get("/manager/reviews/hidden")[0] == 200)

admin = Session()
admin.login("admin@clinic.demo")
status, body, _ = admin.get("/admin")
check("admin overview opens", status == 200 and "Обзор клиники" in body)

print()
if failures:
    print(f"{len(failures)} check(s) failed: {', '.join(failures)}")
    sys.exit(1)
print("All smoke checks passed")
