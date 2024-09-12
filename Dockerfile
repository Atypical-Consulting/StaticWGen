# Use a lightweight nginx image to serve the static files
FROM nginx:alpine

# Copy the static website files to the nginx web root
COPY ./output /usr/share/nginx/html

# Expose port 80 for serving the site
EXPOSE 80

# Start nginx server
CMD ["nginx", "-g", "daemon off;"]
