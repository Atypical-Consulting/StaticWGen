# Pin nginx version for reproducible builds
FROM nginx:1.29-alpine

# Copy custom nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy the static website files to the nginx web root
COPY ./output /usr/share/nginx/html

# Fix permissions for non-root user
RUN chown -R nginx:nginx /usr/share/nginx/html && \
    chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /var/log/nginx && \
    touch /var/run/nginx.pid && \
    chown -R nginx:nginx /var/run/nginx.pid

# Run as non-root user
USER nginx

# Expose port 80 for serving the site
EXPOSE 80

# Health check for container orchestrators
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget -qO- http://localhost:80/ || exit 1

# Start nginx server
CMD ["nginx", "-g", "daemon off;"]
