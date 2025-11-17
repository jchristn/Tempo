#!/bin/bash
# Cleanup script for Test.Automated
# Deletes leftover test database files and log directories

echo "Cleaning up test artifacts..."
echo ""

# Delete test database files
db_count=0
for file in metrics_*.db; do
    if [ -f "$file" ]; then
        rm -f "$file"
        ((db_count++))
    fi
done

if [ $db_count -gt 0 ]; then
    echo "Deleted $db_count test database file(s)"
else
    echo "No test database files found"
fi

# Delete test log directories
log_count=0
for dir in test_logs_*; do
    if [ -d "$dir" ]; then
        rm -rf "$dir"
        ((log_count++))
    fi
done

if [ $log_count -gt 0 ]; then
    echo "Deleted $log_count test log director(ies)"
else
    echo "No test log directories found"
fi

# Delete test request directories
req_count=0
for dir in test_requests_*; do
    if [ -d "$dir" ]; then
        rm -rf "$dir"
        ((req_count++))
    fi
done

if [ $req_count -gt 0 ]; then
    echo "Deleted $req_count test request director(ies)"
else
    echo "No test request directories found"
fi

echo ""
echo "Cleanup complete!"
