This is the code for the .env file you need.
You can copy the code, put it in a new file called .env, and complete the information.
To complete your infomration, you need to create your own account for the smtp email server and your own JSON Web Token.
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
Jwt__Audience=PSE-Green-Clients
