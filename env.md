This is the code for the .env file you need. You should create your own accounts for the email and JWT.
Your final .env file should be in the root folder of the project
```# Environment configuration for PSE-Green-Code
# Keep this file secure and do NOT commit it to version control.

ConnectionStrings__Default=

# Email settings
EmailSettings__Host=
EmailSettings__Port=
EmailSettings__Username=
EmailSettings__Password=
EmailSettings__EnableSsl=true
EmailSettings__FromEmail=
EMAIL_SMTP_USERNAME=
EMAIL_SMTP_PASSWORD=

# Application settings
AppSettings__BaseUrl=https://localhost:6969
AppSettings__EnforceAuth=true
AppSettings__AllowHttp=false
AppSettings__AllowedOrigins__0=https://localhost:6969
AppSettings__RateLimit__Enabled=true
AppSettings__RateLimit__Requests=60
AppSettings__RateLimit__WindowSeconds=60

# JWT
Jwt__Key=''
Jwt__Issuer=PSE-Green
Jwt__Audience=PSE-Green-Clients```
